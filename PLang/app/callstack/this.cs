using app.error;
using @bool = global::app.type.item.@bool.@this;
using number = global::app.type.item.number.@this;

namespace app.callstack;

/// <summary>
/// Per-app call tree. Owned by <c>App.CallStack</c> — moved here from
/// <c>Actor.Context.CallStack</c> because it's an observability concern, not an actor one.
///
/// Structural data (Action, Caller, Errors) is always populated — the cost of the
/// thin push/pop is ~50ns per action and means errors get a useful trace without any flag.
/// Richer capture is fine-grained per-knob (<see cref="Timing"/>, <see cref="Diff"/>,
/// <see cref="DeepDiff"/>, <see cref="Tags"/>, <see cref="History"/>).
///
/// AsyncLocal &lt;Call&gt; is the only shared mutable state — fork-safe by construction so
/// parallel goal.call branches each maintain their own Current without cloning context.
/// </summary>
public sealed partial class @this
{
    // Instance-level — each CallStack has its own AsyncLocal flow. Tests can spin up
    // multiple CallStacks in the same process without polluting each other's Current.
    private readonly AsyncLocal<call.@this?> _current = new();
    private call.@this? _root;

    /// <summary>What this stack captures (<c>%!app.callstack.setting%</c>) — held, since every push reads it;
    /// the app builds it again when a value under its path is written.</summary>
    public setting.@this Setting { get; internal set; } = new();

    // An open diff scope (DiffScope) turns Diff on for as long as it's open, whatever the setting says.
    private int _diffScopes;

    public @bool  Timing    => Setting.Timing;
    public @bool  Diff      => Volatile.Read(ref _diffScopes) > 0 ? @bool.True : Setting.Diff;
    public @bool  DeepDiff  => Setting.DeepDiff;
    public @bool  Tags      => Setting.Tags;
    public @bool  History   => Setting.History;
    public number MaxFrames => Setting.MaxFrames;

    /// <summary>
    /// Run-wide accumulator of every error observed (handled or unhandled). Survives Pop.
    /// See <see cref="audit.@this"/> for thread-safety + lifecycle.
    /// </summary>
    public audit.@this Audit { get; } = new();

    /// <summary>
    /// Optional Variables source for diff capture — the store a Call captures when pushed with none of its own
    /// (<see cref="Push"/>'s <c>variables</c>); the store records each change here (<c>Record</c>) and a Call
    /// pushed while <see cref="Diff"/> is on keeps its store's.
    /// </summary>
    public Variables? Variables { get; internal set; }

    /// <summary>
    /// The current Call in this async context. Null when no Push has happened on this branch.
    /// </summary>
    public call.@this? Current => _current.Value;

    /// <summary>The goal in play on this branch (<c>%!goal%</c>) — the current frame's; null before any frame is
    /// pushed. The stack is where it already lives, so nothing stores it a second time.</summary>
    public global::app.goal.@this? Goal => _current.Value?.Goal;

    /// <summary>The step in play on this branch (<c>%!step%</c>) — the current frame's; null outside any step
    /// (a goal's own frame, an action composed in C#, before any frame is pushed).</summary>
    public global::app.goal.step.@this? Step => _current.Value?.Step;

    /// <summary>
    /// The error in play — what PLang reads as <c>%!error%</c>. Walks <c>Caller</c> outward from
    /// <see cref="Current"/> and answers with the first frame that holds an unrecovered error;
    /// null when nothing on the live chain has failed. The stack is where the error already
    /// lives, so nothing stores it a second time: the frame is the scope.
    /// </summary>
    public global::app.error.Error? Error
    {
        get
        {
            for (var node = _current.Value; node != null; node = node.Caller)
                if (node.Error is { } error) return error;
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
    public call.@this Push(global::app.goal.@this goal) => Push(goal, null, null, null);

    /// <summary>Pushes the frame of a step starting: its goal and itself.</summary>
    public call.@this Push(global::app.goal.step.@this step) => Push(step.Goal, step, null, null);

    /// <summary>Pushes the frame of an action starting: its step's goal, its step and itself — an action composed
    /// in C# holds no step. <paramref name="variables"/> is the store for diff capture (when Diff is on).</summary>
    public call.@this Push(global::app.goal.step.action.@this action, Variables? variables = null)
        => Push(action.Step?.Goal, action.Step, action, variables);

    private call.@this Push(global::app.goal.@this? goal, global::app.goal.step.@this? step,
        global::app.goal.step.action.@this? action, Variables? variables)
    {
        var caller = _current.Value;

        // the caller's depth is its own fact: one more frame past MaxDepth is the overflow
        if (caller != null && caller.Depth >= MaxDepth)
            throw new CallStackOverflowException(MaxDepth);

        var call = new call.@this(goal, step, action, caller, this, caller, variables ?? Variables);

        // Children owns its own lock + FIFO eviction policy.
        caller?.Children.Add(call);

        // Track the live run's root: reassign whenever the new Push has no caller, so
        // %!callStack.Root% reflects the current run rather than a stale prior root that
        // may have already been popped (and disposed).
        if (caller == null) _root = call;
        _current.Value = call;
        return call;
    }

    /// <summary>
    /// The depth limit met, as the run's error: <paramref name="goal"/> and <paramref name="step"/> are where the
    /// frame that never pushed would have run, and the error carries the chain it would have joined. Filed in the
    /// audit.
    /// </summary>
    public global::app.error.Error Overflow(CallStackOverflowException ex, global::app.goal.@this? goal, global::app.goal.step.@this? step)
    {
        var error = new ServiceError(ex.Message, "CallStackOverflow", 500)
        {
            Step = step,
            Goal = goal,
            CallFrames = Current?.SnapshotChain() ?? Array.Empty<call.@this>(),
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
