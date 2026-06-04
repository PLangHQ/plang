using System.Collections.Concurrent;
using app;
using app.variable;
using app.@event;
using Goal = app.goal.@this;
using Action = app.goal.steps.step.actions.action.@this;
using Setup = app.goal.setup.@this;
using TraceContext = app.actor.context.trace.@this;
using app.error;
using ActorType = app.actor.@this;
using CallStackType = app.callstack.@this;
namespace app.actor.context;

/// <summary>
/// Request-level context for a single PLang execution.
/// Created per request/goal execution and contains execution-specific state.
/// </summary>
public sealed class @this : IDisposable
{
    private readonly ConcurrentDictionary<string, object> _data = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>
    /// Unique identifier for this execution context.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Per-context trace identity. Born with the Context. Used to group diagnostic
    /// output (trace JSON, LLM debug files) under a single id for one execution.
    /// Accessible from PLang as <c>%!trace.id%</c>.
    /// </summary>
    public TraceContext Trace { get; } = new();

    /// <summary>
    /// Reference to the app.
    /// </summary>
    public app.@this App { get; }

    /// <summary>
    /// Variables for this execution.
    /// </summary>
    public Variables Variable { get; }

    /// <summary>
    /// The app's call tree. Read-through to <c>App.CallStack</c> — single tree per run,
    /// fork-safe via AsyncLocal. PLang <c>%!callStack%</c> still resolves through this getter.
    /// </summary>
    public CallStackType? CallStack => App?.CallStack;

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
    /// Pushes a timeout CTS so all sub-calls use it. Used by the timeout.after modifier.
    /// </summary>
    public void PushCancellation(CancellationTokenSource cts) => _cancellationStack.Push(cts);

    /// <summary>
    /// Pops the timeout CTS, restoring the previous cancellation token.
    /// </summary>
    public void PopCancellation() { if (_cancellationStack.Count > 0) _cancellationStack.Pop(); }

    /// <summary>
    /// Parent context (if this is a child execution).
    /// </summary>
    public @this? Parent { get; }

    /// <summary>
    /// The actor that owns this context (if any).
    /// </summary>
    public ActorType? Actor { get; internal set; }

    /// <summary>
    /// Event bindings registered on this context.
    /// Each actor's context has its own event collection.
    /// </summary>
    public global::app.@event.list.@this Events { get; } = new();

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
    public data.@this? EventOverride { get; set; }

    /// <summary>
    /// Test context — a Data with Properties for results, summary, etc.
    /// Set when --test flag is active. Accessible via %!test%.
    /// Properties are extensible — results, summary can be GoalCalls.
    /// </summary>
    public data.@this? Test { get; set; }

    /// <summary>
    /// The current event context. Set by the source generator when a parameter implements IEvent.
    /// Accessible via %!event%. Contains .step (triggering step), .phase, etc.
    /// Null when not in an event handler.
    /// </summary>
    public module.EventContext? Event { get; set; }

    /// <summary>
    /// Set during setup execution, null otherwise.
    /// Steps check this to implement run-once semantics.
    /// Propagates through goal.call since Goal.RunAsync uses the same context object.
    /// </summary>
    public Setup? Setup { get; set; }

    /// <summary>
    /// Goal-scoped settings storage. Lazy-initialized when a settings handler writes
    /// to this context. Keys are "module.property" format (e.g., "archive.max").
    /// Resolution walks: this.ConfigScope → Parent.ConfigScope → App.Config.Defaults → class default.
    /// </summary>
    public ConfigScope? ConfigScope { get; set; }

    public @this(app.@this app, Variables? variables = null, @this? parent = null, CancellationToken? parentToken = null)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        App = app;
        Variable = variables ?? new Variables();
        Parent = parent;
        CreatedAt = DateTime.UtcNow;
        var linkTo = parentToken ?? parent?.CancellationToken ?? app.ShutdownToken;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(linkTo);
        // Wire event registration to invalidate the resolved-events cache
        Events.OnChanged = InvalidateEventCache;

        // Stamp context on Variables (propagates to all existing Data)
        Variable.Context = this;

        // Register context variables on the Variables
        RegisterContextVariables();
    }

    /// <summary>
    /// Registers context variables (prefixed with !) on the Variables.
    /// </summary>
    private void RegisterContextVariables()
    {
        var vars = Variable;

        // All context variables are lazy — context has app, fetch at request time
        vars.Set(new data.DynamicData("!app", () => App));
        vars.Set(new data.DynamicData("!context", () => this));
        vars.Set(new data.DynamicData("!variables", () => Variable));
        vars.Set(new data.DynamicData("!callStack", () => CallStack));
        vars.Set(new data.DynamicData("!trace", () => Trace));
        vars.Set(new data.DynamicData("!channels", () => Actor?.Channel));
        vars.Set(new data.DynamicData("!serializers", () => Actor!.Channel.Serializers));
        vars.Set(new data.DynamicData("!goal", () => Goal));
        vars.Set(new data.DynamicData("!step", () => Step));
        // %!error% reads from App.Errors.@this — an AsyncLocal scope managed by
        // error.handle.Wrap via using(app.error.Push(caught)) { ... }. Null outside any
        // active recovery scope; in nested handlers each scope sees its own caught error
        // (LIFO restore on dispose). AsyncLocal is parallelism-safe by construction.
        vars.Set(new data.DynamicData("!error", () => App.Error.Error));
        vars.Set(new data.DynamicData("!data", () => App.System.Context.Variable.GetValue("data")));
        vars.Set(new data.DynamicData("!event", () => Event ?? App.System?.Context?.Event));
        vars.Set(new data.DynamicData("!test", () => Test));
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
    /// Gets the module-scoped static dictionary for the given module namespace.
    /// Created on first access, persists for the lifetime of this context.
    /// Used by IStatic — actions in the same module share the same dictionary.
    /// </summary>
    public ConcurrentDictionary<string, object?> GetModuleStatic(string moduleNamespace)
    {
        var key = $"__static_{moduleNamespace}__";
        return (ConcurrentDictionary<string, object?>)_data.GetOrAdd(key,
            _ => new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets module static at the specified scope level.
    /// step = step-local (caller manages cleanup), goal = goal-scoped (default),
    /// context = context lifetime, app = app lifetime.
    /// </summary>
    public ConcurrentDictionary<string, object?> GetModuleStatic(string moduleNamespace, string scope)
    {
        var key = $"__static_{moduleNamespace}__";
        return scope.ToLowerInvariant() switch
        {
            "app" => App.Statics.GetBag(key),
            _ => (ConcurrentDictionary<string, object?>)_data.GetOrAdd(key,
                _ => new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase))
        };
    }

    /// <summary>
    /// Creates a child context for nested execution.
    /// Test fixture — production creates contexts only via the ctor in Actor.this.cs.
    /// One Context propagates through the entire goal-call tree of an Actor; child
    /// contexts are unit-test scaffolding for inheritance scenarios (ConfigScope,
    /// Variables, Parent linkage).
    /// </summary>
    public @this CreateChild(Variables? variables = null)
    {
        return new @this(App, variables ?? Variable.Clone(), this);
    }

    /// <summary>
    /// Captures the current Step / Goal / Event / Step.Context anchors and
    /// sets them to the action's for the dispatch's lifetime. On Dispose,
    /// restores. Used by App.Run to scope the dispatch context — parallel
    /// dispatches of the same Step (legal under Task.WhenAll on goal.call)
    /// don't leave a sibling branch's Context pointer leaked on the shared
    /// Step instance.
    /// </summary>
    public IDisposable AnchorScope(Action action)
    {
        var disposable = new AnchorScopeDisposable(this, action);
        Step = action.Step;
        if (Step != null) Step.Context = this;
        Goal = action.Step?.Goal;
        return disposable;
    }

    private readonly struct AnchorScopeDisposable : IDisposable
    {
        private readonly @this _ctx;
        private readonly Action _action;
        private readonly Step? _previousStep;
        private readonly Goal? _previousGoal;
        private readonly module.EventContext? _previousEvent;
        private readonly @this? _previousStepContext;

        public AnchorScopeDisposable(@this context, Action action)
        {
            _ctx = context;
            _action = action;
            _previousStep = context.Step;
            _previousGoal = context.Goal;
            _previousEvent = context.Event;
            _previousStepContext = action.Step?.Context;
        }

        public void Dispose()
        {
            _ctx.Step = _previousStep;
            _ctx.Goal = _previousGoal;
            _ctx.Event = _previousEvent;
            if (_action.Step != null) _action.Step.Context = _previousStepContext;
        }
    }

    /// <summary>
    /// Clones this context with a new Variables.
    /// Test fixture — see CreateChild. Not used by production code; the property
    /// propagation here (IsAsync, Setup, ConfigScope, _data) reflects what existing
    /// tests need, not a Clone/Copy contract for the runtime.
    /// </summary>
    public @this Clone(Variables? variables = null)
    {
        var clone = new @this(App, variables ?? Variable.Clone(), Parent)
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

    // --- Data wrapper cache for structural types (Goal, Step, Action) ---
    // Per-execution: same domain object → same Data wrapper within this context.
    private readonly ConcurrentDictionary<object, data.@this> _wrapperCache = new();

    /// <summary>
    /// Gets or creates a cached Data&lt;T&gt; wrapper for a structural domain object.
    /// Ensures identity: same object → same wrapper within this execution context.
    /// </summary>
    public data.@this<T> GetOrCreate<T>(T key, Func<data.@this<T>> factory) where T : class
    {
        var data = _wrapperCache.GetOrAdd(key, _ => factory());
        return (data.@this<T>)data;
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

            foreach (var b in events.GetMatchingBindings(Trigger.OnBeforeGoalLoad, goalName: goal.Name))
                lifecycle.Before.Add(b);
            foreach (var b in events.GetMatchingBindings(Trigger.OnAfterGoalLoad, goalName: goal.Name))
                lifecycle.After.Add(b);
            foreach (var b in events.GetMatchingBindings(Trigger.BeforeGoal, goalName: goal.Name))
                lifecycle.Before.Add(b);
            foreach (var b in events.GetMatchingBindings(Trigger.AfterGoal, goalName: goal.Name))
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

            foreach (var b in events.GetMatchingBindings(Trigger.OnBeforeStepLoad, goalName: goalName, stepText: step.Text))
                lifecycle.Before.Add(b);
            foreach (var b in events.GetMatchingBindings(Trigger.OnAfterStepLoad, goalName: goalName, stepText: step.Text))
                lifecycle.After.Add(b);
            foreach (var b in events.GetMatchingBindings(Trigger.BeforeStep, goalName: goalName, stepText: step.Text))
                lifecycle.Before.Add(b);
            foreach (var b in events.GetMatchingBindings(Trigger.AfterStep, goalName: goalName, stepText: step.Text))
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

            foreach (var b in events.GetMatchingBindings(Trigger.BeforeAction, module: action.Module, actionName: action.ActionName))
                lifecycle.Before.Add(b);
            foreach (var b in events.GetMatchingBindings(Trigger.AfterAction, module: action.Module, actionName: action.ActionName))
                lifecycle.After.Add(b);

            return lifecycle;
        });
    }

    /// <summary>
    /// Returns matching event GoalCalls for the given owner and phase.
    /// Owner type determines scope: Step → step bindings, Goal → goal bindings.
    /// Used by Event resolver (IEvent) during dot-path traversal.
    /// </summary>
    public List<GoalCall> GetEventBindings(object owner, module.EventPhase phase)
    {
        var events = Events;
        var (beforeType, afterType) = owner switch
        {
            Action action => (Trigger.BeforeAction, Trigger.AfterAction),
            Step step => (Trigger.BeforeStep, Trigger.AfterStep),
            Goal goal => (Trigger.BeforeGoal, Trigger.AfterGoal),
            _ => (Trigger.BeforeStep, Trigger.AfterStep) // fallback
        };

        var eventType = phase == module.EventPhase.Before ? beforeType : afterType;

        string? goalName = owner switch
        {
            Action action => action.Step?.Goal?.Name,
            Step step => step.Goal?.Name,
            Goal goal => goal.Name,
            _ => null
        };
        string? stepText = owner is Step s ? s.Text : null;
        string? moduleName = owner is Action a ? a.Module : null;
        string? actionName = owner is Action a2 ? a2.ActionName : null;

        var bindings = events.GetMatchingBindings(eventType, goalName: goalName, stepText: stepText, module: moduleName, actionName: actionName);
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
/// Provides async-local access to @this.
/// </summary>
public interface IContextAccessor
{
    @this? Current { get; set; }
}

/// <summary>
/// Default implementation using AsyncLocal.
/// </summary>
public class @thisAccessor : IContextAccessor
{
    private static readonly AsyncLocal<@this?> _current = new();

    public @this? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
