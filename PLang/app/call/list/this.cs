using app.error;
using @bool = global::app.type.item.@bool.@this;
using number = global::app.type.item.number.@this;

namespace app.call.list;

/// <summary>
/// A context's calls — <c>%!call%</c>: the chain of frames running now (<see cref="Current"/> the innermost), each a
/// goal, a step, an action, or a frame that binds names (a loop's item, a handler's error), each holding its own
/// variables. A task's context has its own chain, its root frame's caller its caller's current frame.
///
/// Structural data (Action, Caller, Errors) is always populated — the cost of the
/// thin push/pop is ~50ns per action and means errors get a useful trace without any flag.
/// Richer capture is fine-grained per-knob (<see cref="Timing"/>, <see cref="Diff"/> and its <c>deep</c>,
/// <see cref="Tags"/>, <see cref="History"/>).
///
/// AsyncLocal &lt;Call&gt; is the only shared mutable state — fork-safe by construction so
/// parallel goal.call branches each maintain their own Current without cloning context.
/// A plang value: it writes itself as <see cref="Output"/> says.
/// </summary>
public sealed partial class @this : global::app.type.item.@this
{
    /// <summary>A structure — navigated by its members.</summary>
    public override bool IsLeaf => false;

    /// <summary>A context's calls are live, as its channels are: a copy of them is them.</summary>
    protected internal override global::app.type.item.@this Clone() => this;

    /// <summary>
    /// The call stack writes one flat form in every view: the frame in play, the goal run's frame and every error
    /// this run observed. Where it is, the error and event in play, the depth limit and the whole run's tree
    /// (<see cref="Root"/>) are one navigation away, never written.
    /// </summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        if (Current is { } current) { writer.Name("current"); await current.Output(writer); }
        if (Scope is { } scope) { writer.Name("scope"); await scope.Output(writer); }
        writer.Name("audit");
        await Audit.Output(writer);
        writer.EndObject();
    }

    // Instance-level — each CallStack has its own AsyncLocal flow. Tests can spin up
    // multiple CallStacks in the same process without polluting each other's Current.
    private readonly AsyncLocal<call.@this?> _current = new();
    private call.@this? _root;

    // The settings these calls read what they capture through — their context's, reached when a frame is pushed, never
    // at birth (a context's calls are born while the context is, before its settings can be read).
    private readonly Func<global::app.actor.setting.@this> _settings;

    // The app's types, handed to each frame at its push: the frame's own (a call, a binding, an isolated call), its
    // start and end (datetime), and its tags (dict) — each asked of the types when a frame needs it.
    private readonly global::app.type.list.@this _types;
    private global::app.type.@this Binding => _types[typeof(call.binding.@this)];
    private global::app.type.@this Isolated => _types[typeof(call.isolated.@this)];

    /// <summary>The app's datetime type — a frame's start and end are of it.</summary>
    internal global::app.type.@this Datetime => _types[typeof(global::app.type.item.datetime.@this)];

    /// <summary>The span type a frame's run time is: a duration with no form of its own, written in its asker's
    /// standard.</summary>
    internal global::app.type.@this Span => _types[typeof(global::app.type.item.duration.span.@this)];

    /// <summary>The app's dict type — a frame's tags are of it.</summary>
    internal global::app.type.@this Dict => _types[typeof(global::app.type.item.dict.@this)];

    /// <summary>The app's error list type — a frame's errors and the audit are of it.</summary>
    internal global::app.type.@this Errors => _types[typeof(global::app.error.list.@this)];

    /// <summary>The app's property list type — what a program adds to a frame is of it.</summary>
    internal global::app.type.@this Properties => _types[typeof(global::app.type.item.property.list.@this)];

    /// <summary>A context's calls, of the app's <paramref name="types"/>, reading what they capture through its
    /// <paramref name="settings"/>.</summary>
    public @this(global::app.type.list.@this types, Func<global::app.actor.setting.@this> settings) : base(types[typeof(@this)])
    {
        _types = types;
        _settings = settings;
    }

    /// <summary>What this stack captures (<c>%!app.call.setting%</c>), as its settings have it now — kept by them until a
    /// write. A push is handed it by its async caller: a frame is made current where it is pushed, which an awaited push
    /// would undo on its return.</summary>
    [global::app.LlmBuilder]
    public System.Threading.Tasks.ValueTask<setting.@this> Setting() => _settings().Get<setting.@this>();

    // An open diff scope (DiffScope) turns Diff on for as long as it's open, whatever the setting says.
    private int _diffScopes;

    /// <summary>Whether a push captures diffs under <paramref name="setting"/>: an open diff scope says yes whatever
    /// the setting says.</summary>
    internal bool IsDiffing(setting.@this setting) => Volatile.Read(ref _diffScopes) > 0 || setting.Diff.Enabled.Value;

    /// <summary>
    /// Every error observed this run at every frame (handled or unhandled). Survives Pop. Made when first asked — at
    /// the first observed error — the first made kept.
    /// See <see cref="global::app.error.list.@this"/> for thread-safety + lifecycle.
    /// </summary>
    [global::app.LlmBuilder]
    public global::app.error.list.@this Audit
        => _audit ?? LazyInitializer.EnsureInitialized(ref _audit, () => new(Errors));
    private global::app.error.list.@this? _audit;

    /// <summary>
    /// The current Call in this async context. Null when no Push has happened on this branch.
    /// </summary>
    [global::app.LlmBuilder]
    public call.@this? Current => _current.Value;

    /// <summary>The goal in play on this branch (<c>%!goal%</c>) — the current frame's; null before any frame is
    /// pushed. The stack is where it already lives, so nothing stores it a second time.</summary>
    public global::app.goal.@this? Goal => _current.Value?.Goal;

    /// <summary>The step in play on this branch (<c>%!step%</c>) — the current frame's; null outside any step
    /// (a goal's own frame, an action composed in C#, before any frame is pushed).</summary>
    public global::app.goal.step.@this? Step => _current.Value?.Step;

    /// <summary>The frame of the goal run in play (<c>%!callStack.Scope%</c>) — the nearest frame outward from
    /// <see cref="Current"/> pushed for a goal itself (a goal, no step). Its steps' frames come and go inside
    /// it; what lives for the goal's run (its tags) lives here. Null outside any goal.</summary>
    [global::app.LlmBuilder]
    public call.@this? Scope => _current.Value?.Within.LastOrDefault(frame => frame.IsGoal);

    /// <summary>
    /// The error in play — what PLang reads as <c>%!error%</c>. Walks <c>Caller</c> outward from
    /// <see cref="Current"/> and answers with the first frame that holds an unrecovered error;
    /// null when nothing on the live chain has failed. The stack is where the error already
    /// lives, so nothing stores it a second time: the frame is the scope.
    /// </summary>
    public global::app.error.@this? Error
    {
        get
        {
            for (var node = _current.Value; node != null; node = node.Caller)
                if (node.Error is { } error) return error;
            return null;
        }
    }

    /// <summary>
    /// The event in play — what PLang reads as <c>%!event%</c>: the nearest frame outward whose event's bound
    /// call is running. Null outside any such call.
    /// </summary>
    public global::app.data.@this? Event
    {
        get
        {
            for (var node = _current.Value; node != null; node = node.Caller)
                if (node.Event is { } running) return running;
            return null;
        }
    }

    /// <summary>
    /// First Call pushed in this run. Null until first Push.
    /// </summary>
    public call.@this? Root => _root;

    /// <summary>
    /// Maximum depth of the synchronous Caller chain before a runaway is treated as a cycle.
    /// Default 1500 — a goal level is three frames (goal, step, action), so a program recurses about 500
    /// levels. A count can't know the thread's stack (Linux's main thread has 8 MB, Windows' 1 MB), so a frame is
    /// also refused when the stack runs short (<see cref="Push{TState}"/>).
    /// </summary>
    public int MaxDepth { get; init; } = 1500;

    /// <summary>
    /// Pushes a new <see cref="call.@this"/>, sets it as the AsyncLocal Current, appends to
    /// <c>Caller.Children</c>, and enforces the depth limit (MaxDepth) — the only limit: a goal may
    /// call itself, directly or through others.
    /// The returned Call IS <see cref="IAsyncDisposable"/> — use <c>await using</c> for
    /// automatic Pop.
    /// </summary>
    /// <param name="goal">The goal starting — its frame's goal is itself.</param>
    /// <param name="setting">What the stack captures, as its async caller read it (<see cref="Setting"/>).</param>
    /// <param name="names">The variables its frame is born with — a goal call's parameters.</param>
    public call.@this Push(global::app.goal.@this goal, setting.@this setting, IEnumerable<global::app.data.@this>? names = null)
        => Push((goal, names), setting, static (stack, s) => new call.goal.@this(stack._types[typeof(call.goal.@this)], stack, s.goal, s.names));

    /// <summary>Pushes a frame that only binds <paramref name="names"/> (a loop's item, a handler's error) — for
    /// <paramref name="held"/> when its runner supplies them to that one call (a tool call's state).</summary>
    public call.@this Push(IEnumerable<global::app.data.@this>? names, setting.@this setting, global::app.goal.step.action.@this? held = null)
        => Enter(new call.binding.@this(Binding, _current.Value, this, names, held), setting);

    /// <summary>Pushes a frame with memory of its own, born with <paramref name="names"/>: every write under it stays
    /// in it (a tool's goal, a shortcut's). A task's run starts its chain with one whose <paramref name="caller"/> is
    /// the frame its caller was in — reads fall through to the caller's frames and memory.</summary>
    public call.@this Isolate(IEnumerable<global::app.data.@this>? names, setting.@this setting,
        global::app.goal.step.action.@this? held = null, call.@this? caller = null)
        => Enter(new call.isolated.@this(Isolated, caller ?? _current.Value, this, names, held), setting);

    /// <summary>Pushes the frame of a step starting: its goal and itself.</summary>
    public call.@this Push(global::app.goal.step.@this step, setting.@this setting)
        => Push(step, setting, static (stack, step) => new call.step.@this(stack._types[typeof(call.step.@this)], stack, step));

    /// <summary>Pushes the frame of an action a step holds starting: its step's goal, its step and itself.
    /// <paramref name="variables"/> is the store for diff capture (when Diff is on).</summary>
    public call.@this Push(global::app.goal.step.action.@this action, setting.@this setting, Variables? variables = null)
        => Push((action, variables), setting, static (stack, s) => new call.action.@this(stack._types[typeof(call.action.@this)], stack, s.action, s.variables));

    /// <summary>Pushes the frame of an action composed in C# (it sits in no step's code: nothing of it is left once it has
    /// run). <paramref name="variables"/> is the store for diff capture (when Diff is on).</summary>
    public call.@this Compose(global::app.goal.step.action.@this action, setting.@this setting, Variables? variables = null)
        => Push((action, variables), setting, static (stack, s) => new call.composed.@this(stack._types[typeof(call.composed.@this)], stack, s.action, s.variables));

    // A call frame (a goal's, a step's, an action's) pushed: past MaxDepth (its caller's depth), or with the thread's
    // stack run short, it is the overflow, refused before anything of it is made — a stack that overflows can't be
    // caught and ends the process. Its state rides in, so the making captures nothing.
    private call.@this Push<TState>(TState state, setting.@this setting, Func<@this, TState, call.@this> make)
    {
        if (_current.Value is { } caller)
        {
            if (caller.Depth >= MaxDepth) throw new CallStackOverflowException(MaxDepth);
            if (!System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack())
                throw new CallStackOverflowException((int)caller.Depth,
                    $"Call stack overflow: the thread's stack ran short at {caller.Depth} frames");
        }
        return Enter(make(this, state), setting);
    }

    // A frame's run begins, under the setting its caller read: it becomes current, joins its caller's children
    // (Children owns its own lock + FIFO eviction, by the setting just begun), and a frame that starts an empty chain
    // is the run's root — reassigned each time, so %!call.Root% is the live run's, never a stale one already popped.
    // A pushed frame and one come back from a snapshot enter the same way.
    internal call.@this Enter(call.@this frame, setting.@this setting)
    {
        frame.Begin(setting);
        frame.Caller?.Children.Add(frame);
        if (_current.Value == null) _root = frame;
        _current.Value = frame;
        return frame;
    }

    /// <summary>
    /// The depth limit met, as the run's error: <paramref name="goal"/> and <paramref name="step"/> are where the
    /// frame that never pushed would have run, and the error carries the chain it would have joined. Filed in the
    /// audit.
    /// </summary>
    public global::app.error.@this Overflow(CallStackOverflowException ex, global::app.goal.@this? goal, global::app.goal.step.@this? step)
    {
        var error = new ServiceError(ex.Message, "CallStackOverflow", 500)
        {
            Step = step,
            Goal = goal,
            CallFrames = Current?.Chain ?? Array.Empty<call.@this>(),
            Exception = ex,
        };
        Audit.Add(error);
        return error;
    }

    /// <summary>
    /// Restores AsyncLocal Current to <paramref name="previous"/> if <paramref name="leaving"/>
    /// is still the active value. Called by <see cref="call.@this.DisposeAsync"/>.
    /// </summary>
    internal void RestoreCurrent(call.@this leaving, call.@this? previous)
    {
        if (ReferenceEquals(_current.Value, leaving))
            _current.Value = previous;
    }

}
