using app.error;
using ActionEntity = app.goal.steps.step.actions.action.@this;

namespace app.callstack;

/// <summary>
/// Per-app call tree. Owned by <c>App.CallStack</c> — moved here from
/// <c>Actor.Context.CallStack</c> because it's an observability concern, not an actor one.
///
/// Structural data (Action, Caller, Errors) is always populated — the cost of the
/// thin push/pop is ~50ns per action and means errors get a useful trace without any flag.
/// Richer capture is fine-grained per-flag (<see cref="Flags"/>): timing, diff,
/// deepDiff, tags, history.
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

    /// <summary>
    /// Per-property gates for richer Call data capture. See <see cref="Flags"/>.
    /// Settable so <see cref="app.modules.debug.@this.Apply"/> can update it from <c>--debug</c>
    /// after construction; otherwise stays at <see cref="Flags.Default"/>.
    ///
    /// Concurrency note: <see cref="Flags"/> is a multi-field <c>record struct</c>;
    /// reassigning this property mid-run via Debug.Apply is a non-atomic copy. A reader
    /// (e.g. Children.Add evaluating <c>History</c> + <c>MaxFrames</c>) executing during the
    /// reassignment can observe a torn struct. Worst case is one off-by-one FIFO eviction
    /// decision — no data loss, no exception. Practically rare since debug mode is set at
    /// startup or pause/resume, not steady-state. Accepted as documented.
    /// </summary>
    public Flags Flags { get; set; } = Flags.Default;

    /// <summary>
    /// Run-wide accumulator of every error observed (handled or unhandled). Survives Pop.
    /// See <see cref="audit.@this"/> for thread-safety + lifecycle.
    /// </summary>
    public audit.@this Audit { get; } = new();

    /// <summary>
    /// Optional Variables source for diff capture. Set by <see cref="Push"/> via the
    /// per-call <c>variables</c> argument; the active Call subscribes to its <c>OnSet</c>
    /// when <see cref="Flags.Diff"/> is on.
    /// </summary>
    public Variables? Variables { get; set; }

    /// <summary>
    /// The current Call in this async context. Null when no Push has happened on this branch.
    /// </summary>
    public call.@this? Current => _current.Value;

    /// <summary>
    /// First Call pushed in this run. Null until first Push.
    /// </summary>
    public call.@this? Root => _root;

    /// <summary>
    /// Maximum depth of the synchronous Caller chain before a runaway is treated as a cycle.
    /// Default 1000 — high enough that legitimate recursion has headroom but low enough
    /// that real infinite loops don't blow the stack.
    /// </summary>
    public int MaxDepth { get; init; } = 1000;

    /// <summary>
    /// Pushes a new <see cref="call.@this"/>, sets it as the AsyncLocal Current, appends to
    /// <c>Caller.Children</c>, and enforces cycle detection (MaxDepth + ContainsGoal).
    /// The returned Call IS <see cref="IAsyncDisposable"/> — use <c>await using</c> for
    /// automatic Pop.
    /// </summary>
    /// <param name="action">The action being dispatched.</param>
    /// <param name="variables">Variables instance for diff capture (when Flags.Diff is on).</param>
    public call.@this Push(ActionEntity action, Variables? variables = null)
    {
        var caller = _current.Value;

        // Cycle detection — depth and goal-cycle both trip CallStackOverflowException.
        if (caller != null)
        {
            int depth = 0;
            var node = caller;
            while (node != null) { depth++; node = node.Caller; }
            if (depth >= MaxDepth)
                throw new CallStackOverflowException(MaxDepth);
        }

        // Goal-boundary cycle: only enforce when this Push *crosses* into a different goal
        // than the caller. Two actions inside the same goal share an identity — that's
        // not a cycle, that's just sequencing (the orchestrator dispatching elseif, retry
        // dispatching same action, foreach body, etc.). A real cycle is goal A → goal B →
        // goal A, where the new entry's goal is already on the stack via a prior boundary.
        // PrPath is the identity (goal Name can collide across an app's goal tree).
        var goalPath = action.Step?.Goal?.PrPath;
        var callerGoalPath = caller?.Action.Step?.Goal?.PrPath;
        if (goalPath != null
            && !goalPath.Equals(callerGoalPath)
            && ContainsGoal(goalPath))
            throw new CallStackOverflowException(MaxDepth);

        var call = new call.@this(action, caller, this, Flags, caller, variables ?? Variables);

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
    /// Restores AsyncLocal Current to <paramref name="previous"/> if <paramref name="leaving"/>
    /// is still the active value. Called by <see cref="call.@this.DisposeAsync"/>.
    /// </summary>
    internal void RestoreCurrent(call.@this leaving, call.@this? previous)
    {
        if (ReferenceEquals(_current.Value, leaving))
            _current.Value = previous;
    }

    /// <summary>
    /// True if any Call in the synchronous Caller chain belongs to a goal with the given
    /// <paramref name="prPath"/>. Case-insensitive. PrPath is the goal's stable identity —
    /// goal Name alone can collide across an app's goal tree. Used by Push to enforce
    /// indirect goal-cycle detection.
    /// </summary>
    public bool ContainsGoal(global::app.types.path.@this prPath)
    {
        var node = _current.Value;
        while (node != null)
        {
            var path = node.Action.Step?.Goal?.PrPath;
            if (path != null && path.Equals(prPath))
                return true;
            node = node.Caller;
        }
        return false;
    }
}
