using System.Collections.Concurrent;
using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Events;
using Goal = PLang.Runtime2.Engine.Goals.Goal.@this;
using Action = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this;
using Setup = PLang.Runtime2.Engine.Goals.Setup.@this;
namespace PLang.Runtime2.Engine.Context;

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
    public Engine.@this Engine { get; }

    /// <summary>
    /// Memory stack for this execution.
    /// </summary>
    public MemoryStack MemoryStack { get; }

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
    /// System-level event scope.
    /// </summary>
    public EventScope System { get; }

    /// <summary>
    /// User-level event scope.
    /// </summary>
    public EventScope User { get; }

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

    public PLangContext(Engine.@this engine, MemoryStack? memoryStack = null, PLangContext? parent = null)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        Engine = engine;
        MemoryStack = memoryStack ?? new MemoryStack();
        Parent = parent;
        CreatedAt = DateTime.UtcNow;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(engine.ShutdownToken);
        System = new EventScope();
        User = new EventScope();

        // Wire event registration to invalidate the resolved-events cache
        System.Events.OnChanged = InvalidateEventCache;
        User.Events.OnChanged = InvalidateEventCache;

        // Stamp context on MemoryStack (propagates to all existing Data)
        MemoryStack.Context = this;

        // Register context variables on the memory stack
        RegisterContextVariables();
    }

    /// <summary>
    /// Registers context variables (prefixed with !) on the memory stack.
    /// </summary>
    private void RegisterContextVariables()
    {
        var ms = MemoryStack;

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
    public PLangContext CreateChild(MemoryStack? memoryStack = null)
    {
        return new PLangContext(Engine, memoryStack ?? MemoryStack.Clone(), this);
    }

    /// <summary>
    /// Clones this context with a new memory stack.
    /// </summary>
    public PLangContext Clone(MemoryStack? memoryStack = null)
    {
        var clone = new PLangContext(Engine, memoryStack ?? MemoryStack.Clone(), Parent)
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
            var userEvents = User.Events;

            foreach (var b in userEvents.GetMatchingBindings(EventType.OnBeforeGoalLoad, goalName: goal.Name))
                lifecycle.Before.Add(b);
            foreach (var b in userEvents.GetMatchingBindings(EventType.OnAfterGoalLoad, goalName: goal.Name))
                lifecycle.After.Add(b);
            foreach (var b in userEvents.GetMatchingBindings(EventType.BeforeGoal, goalName: goal.Name))
                lifecycle.Before.Add(b);
            foreach (var b in userEvents.GetMatchingBindings(EventType.AfterGoal, goalName: goal.Name))
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
            var userEvents = User.Events;
            var goalName = step.Goal?.Name;

            foreach (var b in userEvents.GetMatchingBindings(EventType.OnBeforeStepLoad, goalName: goalName, stepText: step.Text))
                lifecycle.Before.Add(b);
            foreach (var b in userEvents.GetMatchingBindings(EventType.OnAfterStepLoad, goalName: goalName, stepText: step.Text))
                lifecycle.After.Add(b);
            foreach (var b in userEvents.GetMatchingBindings(EventType.BeforeStep, goalName: goalName, stepText: step.Text))
                lifecycle.Before.Add(b);
            foreach (var b in userEvents.GetMatchingBindings(EventType.AfterStep, goalName: goalName, stepText: step.Text))
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
            var userEvents = User.Events;

            foreach (var b in userEvents.GetMatchingBindings(EventType.BeforeAction, module: action.Module, actionName: action.ActionName))
                lifecycle.Before.Add(b);
            foreach (var b in userEvents.GetMatchingBindings(EventType.AfterAction, module: action.Module, actionName: action.ActionName))
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
        var userEvents = User.Events;
        var (beforeType, afterType) = owner switch
        {
            Step step => (EventType.BeforeStep, EventType.AfterStep),
            Goal goal => (EventType.BeforeGoal, EventType.AfterGoal),
            _ => (EventType.BeforeStep, EventType.AfterStep) // fallback
        };

        var eventType = phase == modules.EventPhase.Before ? beforeType : afterType;

        string? goalName = owner switch
        {
            Step step => step.Goal?.Name,
            Goal goal => goal.Name,
            _ => null
        };
        string? stepText = owner is Step s ? s.Text : null;

        var bindings = userEvents.GetMatchingBindings(eventType, goalName: goalName, stepText: stepText);
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
