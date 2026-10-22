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
    // start and end (datetime), and its tags (dict).
    private readonly global::app.type.@this _call;
    private readonly global::app.type.@this _binding;
    private readonly global::app.type.@this _isolated;
    private readonly global::app.type.@this _datetime;
    private readonly global::app.type.@this _dict;

    /// <summary>The app's datetime type — a frame's start and end are of it.</summary>
    internal global::app.type.@this Datetime => _datetime;

    /// <summary>The app's dict type — a frame's tags are of it.</summary>
    internal global::app.type.@this Dict => _dict;

    /// <summary>A context's calls, of the app's <paramref name="types"/>, reading what they capture through its
    /// <paramref name="settings"/>; <paramref name="audit"/> is where every error they observe is kept.</summary>
    public @this(global::app.type.list.@this types,
        Func<global::app.actor.setting.@this> settings, global::app.error.list.@this audit) : base(types[typeof(@this)])
    {
        _call = types[typeof(call.@this)];
        _binding = types[typeof(call.binding.@this)];
        _isolated = types[typeof(call.isolated.@this)];
        _datetime = types[typeof(global::app.type.item.datetime.@this)];
        _dict = types[typeof(global::app.type.item.dict.@this)];
        _settings = settings;
        Audit = audit;
    }

    /// <summary>What this stack captures (<c>%!app.call.setting%</c>), as its settings have it now — kept by them until a
    /// write. A push is handed it by its async caller: a frame is made current where it is pushed, which an awaited push
    /// would undo on its return.</summary>
    [global::app.LlmBuilder]
    public System.Threading.Tasks.ValueTask<setting.@this> Setting() => _settings().Get<setting.@this>();

    // An open diff scope (DiffScope) turns Diff on for as long as it's open, whatever the setting says.
    private int _diffScopes;

    public async System.Threading.Tasks.ValueTask<@bool> Timing() => (await Setting()).Timing;
    public async System.Threading.Tasks.ValueTask<@bool> Diff() => IsDiffing(await Setting()) ? @bool.True : @bool.False;

    /// <summary>Whether a push captures diffs under <paramref name="setting"/>: an open diff scope says yes whatever
    /// the setting says.</summary>
    internal bool IsDiffing(setting.@this setting) => Volatile.Read(ref _diffScopes) > 0 || setting.Diff.Enabled.Value;
    public async System.Threading.Tasks.ValueTask<@bool> Tags() => (await Setting()).Tags;
    public async System.Threading.Tasks.ValueTask<@bool> History() => (await Setting()).History;

    /// <summary>
    /// Every error observed this run at every frame (handled or unhandled). Survives Pop.
    /// See <see cref="global::app.error.list.@this"/> for thread-safety + lifecycle.
    /// </summary>
    [global::app.LlmBuilder]
    public global::app.error.list.@this Audit { get; }

    /// <summary>
    /// Optional Variables source for diff capture — the store a Call captures when pushed with none of its own
    /// (<see cref="Push"/>'s <c>variables</c>); the store records each change here (<c>Record</c>) and a Call
    /// pushed while <see cref="Diff"/> is on keeps its store's.
    /// </summary>
    public Variables? Variables { get; internal set; }

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
    /// levels: headroom for legitimate recursion, low enough that real infinite loops don't blow the stack.
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
        => Enter(new call.@this(_call, this, goal, setting, names));

    /// <summary>Pushes a frame that only binds <paramref name="names"/> (a loop's item, a handler's error) — for
    /// <paramref name="held"/> when its runner supplies them to that one call (a tool call's state).</summary>
    public call.@this Push(IEnumerable<global::app.data.@this>? names, setting.@this setting, global::app.goal.step.action.@this? held = null)
        => Enter(new call.binding.@this(_binding, _current.Value, this, names, held, setting));

    /// <summary>Pushes a frame with memory of its own, born with <paramref name="names"/>: every write under it stays
    /// in it (a tool's goal, a shortcut's). A task's run starts its chain with one whose <paramref name="caller"/> is
    /// the frame its caller was in — reads fall through to the caller's frames and memory.</summary>
    public call.@this Isolate(IEnumerable<global::app.data.@this>? names, setting.@this setting,
        global::app.goal.step.action.@this? held = null, call.@this? caller = null)
        => Enter(new call.isolated.@this(_isolated, caller ?? _current.Value, this, names, held, setting));

    /// <summary>Pushes the frame of a step starting: its goal and itself.</summary>
    public call.@this Push(global::app.goal.step.@this step, setting.@this setting)
        => Enter(new call.@this(_call, this, step, setting));

    /// <summary>Pushes the frame of an action starting: its step's goal, its step and itself — an action composed
    /// in C# holds no step. <paramref name="variables"/> is the store for diff capture (when Diff is on).</summary>
    public call.@this Push(global::app.goal.step.action.@this action, setting.@this setting, Variables? variables = null)
        => Enter(new call.@this(_call, this, action, setting, variables));

    // The frame a frame pushed now is called from — the current one; past MaxDepth (the caller's own depth) the
    // overflow.
    internal call.@this? Caller()
    {
        var caller = _current.Value;
        if (caller != null && caller.Depth >= MaxDepth) throw new CallStackOverflowException(MaxDepth);
        return caller;
    }

    // The frame becomes current: it joins its caller's children (Children owns its own lock + FIFO eviction), and a
    // frame that starts an empty chain is the run's root — reassigned each time, so %!call.Root% is the live run's,
    // never a stale one already popped.
    private call.@this Enter(call.@this frame)
    {
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
