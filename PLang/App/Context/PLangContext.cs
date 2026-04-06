using System.Collections.Concurrent;
using App;
using App.Errors;
using App.Variables;
using App.Events;
using Goal = App.Goals.Goal.@this;
using Action = App.Goals.Goal.Steps.Step.Actions.Action.@this;
using Setup = App.Goals.Setup.@this;
namespace App.Context;

/// <summary>
/// Request-level context for a single PLang execution.
/// Created per request/goal execution and contains execution-specific state.
/// </summary>
public sealed class PLangContext : IDisposable
{
    private readonly ConcurrentDictionary<string, object> _data = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>
    /// Unique identifier for this execution context.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Reference to the engine.
    /// </summary>
    public App.@this Engine { get; }

    /// <summary>
    /// Memory stack for this execution.
    /// </summary>
    public Variables.@this Variables { get; }

    /// <summary>
    /// Call stack for this execution.
    /// </summary>
    public CallStack.@this? CallStack { get; set; }

    /// <summary>
    /// Whether this is an async execution.
    /// </summary>
    public bool IsAsync { get; set; }

    /// <summary>
    /// When this context was created.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Cancellation token for this execution.
    /// </summary>
    public CancellationToken CancellationToken =>
        _cancellationStack.Count > 0 ? _cancellationStack.Peek().Token : (_cts?.Token ?? CancellationToken.None);
    private CancellationTokenSource? _cts;
    private readonly Stack<CancellationTokenSource> _cancellationStack = new();

    /// <summary>
    /// Pushes a timeout CTS so all sub-calls use it. Called by engine.execute for step.Timeout.
    /// </summary>
    public void PushCancellation(CancellationTokenSource cts) => _cancellationStack.Push(cts);

    /// <summary>
    /// Pops the timeout CTS, restoring the previous cancellation token.
    /// </summary>
    public void PopCancellation() { if (_cancellationStack.Count > 0) _cancellationStack.Pop(); }

    /// <summary>
    /// Parent context (if this is a child execution).
    /// </summary>
    public PLangContext? Parent { get; }

    /// <summary>
    /// The actor that owns this context (if any).
    /// </summary>
    public Actor? Actor { get; internal set; }

    /// <summary>
    /// Event bindings registered on this context.
    /// Each actor's context has its own event collection.
    /// </summary>
    public EngineEvents Events { get; } = new();

    /// <summary>
    /// The goal currently being executed.
    /// </summary>
    public Goal? Goal { get; set; }

    /// <summary>
    /// The step currently being executed.
    /// </summary>
    public Step? Step { get; set; }

    /// <summary>
    /// Set by event.skipAction to override the current action's result.
    /// Cleared by EventBinding.Run after reading.
    /// </summary>
    public Data? EventOverride { get; set; }

    /// <summary>
    /// The current error being handled. Set by error.check before calling the error goal.
    /// Accessible via %!error%. Falls back to CallStack.Current.Error when callstack is active.
    /// </summary>
    public IError? CurrentError { get; set; }

    /// <summary>
    /// Test context — a Data with Properties for results, summary, etc.
    /// Set when --test flag is active. Accessible via %!test%.
    /// Properties are extensible — results, summary can be GoalCalls.
    /// </summary>
    public Data? Test { get; set; }

    /// <summary>
    /// The current event context. Set by the source generator when a parameter implements IEvent.
    /// Accessible via %!event%. Contains .step (triggering step), .phase, etc.
    /// Null when not in an event handler.
    /// </summary>
    public modules.EventContext? Event { get; set; }

    /// <summary>
    /// Set during setup execution, null otherwise.
    /// Steps check this to implement run-once semantics.
    /// Propagates through goal.call since Goal.RunAsync uses the same context object.
    /// </summary>
    public Setup? Setup { get; set; }

    /// <summary>
    /// Goal-scoped settings storage. Lazy-initialized when a settings handler writes
    /// to this context. Keys are "module.property" format (e.g., "archive.max").
    /// Resolution walks: this.ConfigScope → Parent.ConfigScope → Engine.Config.Defaults → class default.
    /// </summary>
    public Config.Scope? ConfigScope { get; set; }

    public PLangContext(App.@this engine, Variables.@this? memoryStack = null, PLangContext? parent = null)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        Engine = engine;
        Variables = memoryStack ?? new Variables.@this();
        Parent = parent;
        CreatedAt = DateTime.UtcNow;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(engine.ShutdownToken);
        // Wire event registration to invalidate the resolved-events cache
        Events.OnChanged = InvalidateEventCache;

        // Stamp context on Variables (propagates to all existing Data)
        Variables.Context = this;

        // Register context variables on the memory stack
        RegisterContextVariables();
    }

    /// <summary>
    /// Registers context variables (prefixed with !) on the memory stack.
    /// </summary>
    private void RegisterContextVariables()
    {
        var ms = Variables;

        // Static references (same object for lifetime of context)
        ms.Put(new Data("!engine", Engine));
        ms.Put(new Data("!context", this));
        ms.Put(new Data("!memoryStack", ms));
        ms.Put(new Data("!fileSystem", Engine.FileSystem));
        ms.Put(new DynamicData("!callStack", () => CallStack));
        ms.Put(new Data("!channels", Engine.Channels));
        ms.Put(new Data("!serializers", Engine.Channels.Serializers));

        // Dynamic references (change per goal/step)
        ms.Put(new DynamicData("!goal", () => Goal));
        ms.Put(new DynamicData("!step", () => Step));
        ms.Put(new DynamicData("!error", () => CurrentError ?? CallStack?.Current?.Error));
        ms.Put(new DynamicData("!data", () => Engine.System.Context.Variables.GetValue("data")));
        ms.Put(new DynamicData("!event", () => Event ?? Engine.System?.Context?.Event));
        ms.Put(new DynamicData("!test", () => Test));
    }

    /// <summary>
    /// Gets or sets a value in the execution context.
    /// </summary>
    public object? this[string key]
    {
        get => _data.TryGetValue(key, out var value) ? value : null;
        set
        {
            if (value == null)
                _data.TryRemove(key, out _);
            else
                _data[key] = value;
        }
    }

    /// <summary>
    /// Gets a typed value from the context.
    /// </summary>
    public T? Get<T>(string key)
    {
        if (_data.TryGetValue(key, out var value) && value is T typed)
            return typed;
        return default;
    }

    /// <summary>
    /// Sets a typed value in the context.
    /// </summary>
    public void Set<T>(string key, T value)
    {
        if (value == null)
            _data.TryRemove(key, out _);
        else
            _data[key] = value;
    }

    /// <summary>
    /// Checks if a key exists.
    /// </summary>
    public bool ContainsKey(string key) => _data.ContainsKey(key);

    /// <summary>
    /// Creates a child context for nested execution.
    /// </summary>
    public PLangContext CreateChild(Variables.@this? memoryStack = null)
    {
        return new PLangContext(Engine, memoryStack ?? Variables.Clone(), this);
    }

    /// <summary>
    /// Clones this context with a new memory stack.
    /// </summary>
    public PLangContext Clone(Variables.@this? memoryStack = null)
    {
        var clone = new PLangContext(Engine, memoryStack ?? Variables.Clone(), Parent)
        {
            IsAsync = IsAsync,
            Setup = Setup,
            ConfigScope = ConfigScope?.Clone()
        };

        foreach (var kvp in _data)
        {
            clone._data[kvp.Key] = kvp.Value;
        }

        return clone;
    }

    private readonly ConcurrentDictionary<object, object> _eventContainers = new();
    private readonly ConcurrentDictionary<string, byte> _activeEventBindings = new();

    /// <summary>
    /// Returns true if the event binding is not already running, and marks it as active.
    /// Used to prevent re-entrant event handler execution.
    /// </summary>
    internal bool TryEnterEvent(string bindingId) => _activeEventBindings.TryAdd(bindingId, 0);

    /// <summary>
    /// Marks an event binding as no longer active.
    /// </summary>
    internal void ExitEvent(string bindingId) => _activeEventBindings.TryRemove(bindingId, out _);

    /// <summary>
    /// Clears the event resolution cache. Must be called when events are registered
    /// during execution so newly added events are picked up on subsequent EventsFor() calls.
    /// </summary>
    public void InvalidateEventCache() => _eventContainers.Clear();

    /// <summary>
    /// Resolves per-context lifecycle for a Goal. Lazy-resolves from User.Events on first call, cached on context.
    /// </summary>
    public Lifecycle LifecycleFor(Goal goal)
    {
        return (Lifecycle)_eventContainers.GetOrAdd(goal, _ =>
        {
            var lifecycle = new Lifecycle();
            var events = Events;

            foreach (var b in events.GetMatchingBindings(EventType.OnBeforeGoalLoad, goalName: goal.Name))
                lifecycle.Before.Add(b);
            foreach (var b in events.GetMatchingBindings(EventType.OnAfterGoalLoad, goalName: goal.Name))
                lifecycle.After.Add(b);
            foreach (var b in events.GetMatchingBindings(EventType.BeforeGoal, goalName: goal.Name))
                lifecycle.Before.Add(b);
            foreach (var b in events.GetMatchingBindings(EventType.AfterGoal, goalName: goal.Name))
                lifecycle.After.Add(b);

            return lifecycle;
        });
    }

    /// <summary>
    /// Resolves per-context lifecycle for a Step. Lazy-resolves from User.Events on first call, cached on context.
    /// </summary>
    public Lifecycle LifecycleFor(Step step)
    {
        return (Lifecycle)_eventContainers.GetOrAdd(step, _ =>
        {
            var lifecycle = new Lifecycle();
            var events = Events;
            var goalName = step.Goal?.Name;

            foreach (var b in events.GetMatchingBindings(EventType.OnBeforeStepLoad, goalName: goalName, stepText: step.Text))
                lifecycle.Before.Add(b);
            foreach (var b in events.GetMatchingBindings(EventType.OnAfterStepLoad, goalName: goalName, stepText: step.Text))
                lifecycle.After.Add(b);
            foreach (var b in events.GetMatchingBindings(EventType.BeforeStep, goalName: goalName, stepText: step.Text))
                lifecycle.Before.Add(b);
            foreach (var b in events.GetMatchingBindings(EventType.AfterStep, goalName: goalName, stepText: step.Text))
                lifecycle.After.Add(b);

            return lifecycle;
        });
    }

    /// <summary>
    /// Resolves per-context lifecycle for an Action. Lazy-resolves from User.Events on first call, cached on context.
    /// </summary>
    public Lifecycle LifecycleFor(Action action)
    {
        return (Lifecycle)_eventContainers.GetOrAdd(action, _ =>
        {
            var lifecycle = new Lifecycle();
            var events = Events;

            foreach (var b in events.GetMatchingBindings(EventType.BeforeAction, module: action.Module, actionName: action.ActionName))
                lifecycle.Before.Add(b);
            foreach (var b in events.GetMatchingBindings(EventType.AfterAction, module: action.Module, actionName: action.ActionName))
                lifecycle.After.Add(b);

            return lifecycle;
        });
    }

    /// <summary>
    /// Returns matching event GoalCalls for the given owner and phase.
    /// Owner type determines scope: Step → step bindings, Goal → goal bindings.
    /// Used by Event resolver (IEvent) during dot-path traversal.
    /// </summary>
    public List<GoalCall> GetEventBindings(object owner, modules.EventPhase phase)
    {
        var events = Events;
        var (beforeType, afterType) = owner switch
        {
            Action action => (EventType.BeforeAction, EventType.AfterAction),
            Step step => (EventType.BeforeStep, EventType.AfterStep),
            Goal goal => (EventType.BeforeGoal, EventType.AfterGoal),
            _ => (EventType.BeforeStep, EventType.AfterStep) // fallback
        };

        var eventType = phase == modules.EventPhase.Before ? beforeType : afterType;

        string? goalName = owner switch
        {
            Action action => action.Step?.Goal?.Name,
            Step step => step.Goal?.Name,
            Goal goal => goal.Name,
            _ => null
        };
        string? stepText = owner is Step s ? s.Text : null;
        string? module = owner is Action a ? a.Module : null;
        string? actionName = owner is Action a2 ? a2.ActionName : null;

        var bindings = events.GetMatchingBindings(eventType, goalName: goalName, stepText: stepText, module: module, actionName: actionName);
        return bindings
            .Where(b => b.GoalToCall != null)
            .Select(b => b.GoalToCall!)
            .ToList();
    }

    /// <summary>
    /// Requests cancellation of this execution.
    /// </summary>
    public void Cancel()
    {
        _cts?.Cancel();
    }

    /// <summary>
    /// Execution duration.
    /// </summary>
    public TimeSpan Duration => DateTime.UtcNow - CreatedAt;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        // Dispose any disposable items in the dictionary
        foreach (var value in _data.Values)
        {
            if (value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        _data.Clear();
    }
}

/// <summary>
/// Provides async-local access to PLangContext.
/// </summary>
public interface IPLangContextAccessor
{
    PLangContext? Current { get; set; }
}

/// <summary>
/// Default implementation using AsyncLocal.
/// </summary>
public class PLangContextAccessor : IPLangContextAccessor
{
    private static readonly AsyncLocal<PLangContext?> _current = new();

    public PLangContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
