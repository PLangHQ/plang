using System.Collections.Concurrent;
using app;
using app.@event;
using Goal = app.goal.@this;
using Action = app.goal.step.action.@this;
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
    /// This context's call tree. Read-through to the owning <c>Actor.CallStack</c> — each
    /// actor owns its own tree (a cross-actor call is a separate tree, actor-model style),
    /// fork-safe within the actor's flows via AsyncLocal. PLang <c>%!callStack%</c> resolves here.
    /// </summary>
    public CallStackType CallStack => Actor.CallStack;

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
    /// The actor that owns this context. Born with the context — every context
    /// is owned by exactly one actor, passed into the ctor.
    /// </summary>
    public ActorType Actor { get; }

    /// <summary>
    /// Test context — a Data with Properties for results, summary, etc.
    /// Set when --test flag is active. Accessible via %!test%.
    /// Properties are extensible — results, summary can be GoalCalls.
    /// </summary>
    public data.@this? Test { get; set; }

    /// <summary>
    /// Set during setup execution, null otherwise.
    /// Steps check this to implement run-once semantics.
    /// Propagates through goal.call since Goal.RunAsync uses the same context object.
    /// </summary>
    public Setup? Setup { get; set; }

    /// <summary>
    /// The in-memory settings that belong to this context (the <c>%!%</c> door). Born on first
    /// access. Keys are the full tree path (e.g., "http.request.timeout"). A read walks
    /// this → Parent → … → root; a goal-local setting shadows an app-level one.
    /// </summary>
    private global::app.actor.setting.@this? _setting;
    // Scoped door: chains to the parent context's door, or (at a root context) to its actor's — so an
    // in-memory read walks this → parents → the actor's → the system's → [Default].
    public global::app.actor.setting.@this Setting => _setting ??= new(this, Parent?.Setting ?? Actor.Setting);

    public @this(app.@this app, ActorType owner, Variables? variables = null, @this? parent = null, CancellationToken? parentToken = null)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        App = app;
        Actor = owner;
        Variable = variables ?? new Variables(this);
        Parent = parent;
        CreatedAt = DateTime.UtcNow;
        var linkTo = parentToken ?? parent?.CancellationToken ?? app.ShutdownToken;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(linkTo);

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
        vars.Set(new data.DynamicData("!app", asker => asker.Ok(App), this));
        vars.Set(new data.DynamicData("!context", asker => asker.Ok(this), this));
        vars.Set(new data.DynamicData("!variables", asker => asker.Ok(Variable), this));
        vars.Set(new data.DynamicData("!channels", asker => asker.Ok(Actor.Channel), this));
        // the goal and step in play are the current frame's place — the stack is where they already live. The call
        // stack, the trace and the event in play are the app's own (%!callstack%, %!trace%, %!event%).
        vars.Set(new data.DynamicData("!goal", asker => asker.Ok(CallStack.Goal), this));
        vars.Set(new data.DynamicData("!step", asker => asker.Ok(CallStack.Step), this));
        // %!error% reads the CALL STACK. The error is already recorded on the frame that
        // failed, and that frame is still live while its recovery runs (one frame per action,
        // spanning its modifiers), so nothing stores the error a second time. CallStack.Error
        // walks Caller outward for the first frame holding an unrecovered one — nesting
        // shadows for free, and parallel branches don't cross (the stack is AsyncLocal).
        vars.Set(new data.DynamicData("!error", asker => asker.Ok(CallStack.Error), this));
        vars.Set(new data.DynamicData("!data", _ => App.actor.list.System.Context.Variable.Peek("data"), this));
        vars.Set(new data.DynamicData("!test", asker => asker.Ok(Test), this));
    }

    // --- Value births ---
    // A Data is born FROM this context — never constructed context-less and stamped
    // later. The context is the one handle that's always in scope at a real birth
    // (handler result, %var% resolve, deserialize, LLM parse), so it owns the factory.

    /// <summary>An empty success Data, born with this context.</summary>
    public data.@this Ok() => new("", context: this);

    /// <summary>A success Data wrapping <paramref name="value"/>, born with this context.</summary>
    public data.@this Ok(object? value, global::app.type.@this? type = null)
        => new("", value, type, context: this);

    /// <summary>A typed success Data wrapping <paramref name="value"/>, born with this context.</summary>
    public data.@this<T> Ok<T>(T value, global::app.type.@this? type = null)
        where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
        => new("", value, type, context: this);

    /// <summary>A present-null Data, born with this context.</summary>
    public data.@this Null(string name = "")
        => new(name, global::app.type.item.@null.@this.Instance, context: this);

    /// <summary>An error Data carrying <paramref name="error"/>, born with this context. An error
    /// meeting its first run here takes this context as where it happened.</summary>
    public data.@this Error(global::app.error.Error error)
    {
        error.Context ??= this;
        return new("", context: this) { Error = error };
    }

    /// <summary>A typed error Data carrying <paramref name="error"/>, born with this context. An error
    /// meeting its first run here takes this context as where it happened.</summary>
    public data.@this<T> Error<T>(global::app.error.Error error)
        where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        error.Context ??= this;
        return new("", context: this) { Error = error };
    }

    /// <summary>
    /// Borns the Data result of a value computation: <c>Ok</c> on success, or — when the compute
    /// throws an <see cref="app.error.AppException"/> (a value op with no context of its own,
    /// e.g. arithmetic overflow) — the Error it carries. The third born-a-Data
    /// door beside <see cref="Ok{T}"/> / <see cref="Error{T}"/>.
    /// </summary>
    public data.@this<T> Data<T>(System.Func<T> compute)
        where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        try { return Ok<T>(compute()); }
        catch (global::app.error.AppException ex) { return Error<T>(ex.Error); }
    }

    /// <summary>A not-found Data (present reference, <c>IsInitialized == false</c>), born with this context.</summary>
    public data.@this NotFound(string name = "") => data.@this.NotFound(name, this);

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
    /// contexts are unit-test scaffolding for inheritance scenarios (Setting,
    /// Variables, Parent linkage).
    /// </summary>
    public @this CreateChild(Variables? variables = null)
    {
        return new @this(App, Actor, variables ?? Variable.Clone(), this);
    }

    /// <summary>
    /// Clones this context with a new Variables.
    /// Test fixture — see CreateChild. Not used by production code; the property
    /// propagation here (IsAsync, Setup, Setting, _data) reflects what existing
    /// tests need, not a Clone/Copy contract for the runtime.
    /// </summary>
    public @this Clone(Variables? variables = null)
    {
        var clone = new @this(App, Actor, variables ?? Variable.Clone(), Parent)
        {
            IsAsync = IsAsync,
            Setup = Setup,
        };
        clone._setting = _setting?.Clone();

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
    public data.@this<T> GetOrCreate<T>(T key, Func<data.@this<T>> factory) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        var data = _wrapperCache.GetOrAdd(key, _ => factory());
        return (data.@this<T>)data;
    }

    // The bindings running in this context's current flow: a binding doesn't fire inside its own handler, while a
    // parallel flow on the same context (its own async flow) fires it too. Set per flow: a flow forked from one
    // running a binding inherits the set, and leaving a handler restores what the flow held before it.
    private readonly AsyncLocal<System.Collections.Immutable.ImmutableHashSet<object>?> _runningBindings = new();

    /// <summary>True, marking it running in this flow, when <paramref name="binding"/> is not already running in it.</summary>
    internal bool TryEnterEvent(object binding)
    {
        var running = _runningBindings.Value ?? System.Collections.Immutable.ImmutableHashSet.Create<object>(ReferenceEqualityComparer.Instance);
        if (running.Contains(binding)) return false;
        _runningBindings.Value = running.Add(binding);
        return true;
    }

    /// <summary>Marks <paramref name="binding"/> as no longer running in this flow.</summary>
    internal void ExitEvent(object binding)
    {
        if (_runningBindings.Value is { } running) _runningBindings.Value = running.Remove(binding);
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
