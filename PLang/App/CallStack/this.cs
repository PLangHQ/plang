using App.Errors;
using ActionEntity = App.Goals.Goal.Steps.Step.Actions.Action.@this;

namespace App.CallStack;

/// <summary>
/// Per-app call tree. Owned by <c>App.Debug.CallStack</c> — moved here from
/// <c>Actor.Context.CallStack</c> because it's an observability concern, not an actor one.
///
/// Structural data (Action, Caller, Cause, Errors) is always populated — the cost of the
/// thin push/pop is ~50ns per action and means errors get a useful trace without any flag.
/// Richer capture is fine-grained per-flag (<see cref="CallStackFlags"/>): timing, diff,
/// deepDiff, tags, history.
///
/// AsyncLocal &lt;Call&gt; is the only shared mutable state — fork-safe by construction so
/// parallel goal.call branches each maintain their own Current without cloning context.
/// </summary>
public sealed partial class @this
{
    private static readonly AsyncLocal<Call.@this?> _current = new();
    private Call.@this? _root;

    /// <summary>
    /// Per-property gates for richer Call data capture. See <see cref="CallStackFlags"/>.
    /// Settable so <see cref="App.Debug.@this.Apply"/> can update it from <c>--debug</c>
    /// after construction; otherwise stays at <see cref="CallStackFlags.Default"/>.
    /// </summary>
    public CallStackFlags Flags { get; set; } = CallStackFlags.Default;

    /// <summary>
    /// Run-wide accumulator of every error observed (handled or unhandled). Survives Pop.
    /// Replaces today's <c>Errors</c> property with a clearer name.
    /// </summary>
    public List<IError> Audit { get; } = new();

    /// <summary>
    /// Optional Variables source for diff capture. Set by <see cref="Push"/> via the
    /// per-call <c>variables</c> argument; the active Call subscribes to its <c>OnSet</c>
    /// when <see cref="CallStackFlags.Diff"/> is on.
    /// </summary>
    public Variables.@this? Variables { get; set; }

    /// <summary>
    /// The current Call in this async context. Null when no Push has happened on this branch.
    /// </summary>
    public Call.@this? Current => _current.Value;

    /// <summary>
    /// First Call pushed in this run. Null until first Push.
    /// </summary>
    public Call.@this? Root => _root;

    /// <summary>
    /// Maximum depth of the synchronous Caller chain before a runaway is treated as a cycle.
    /// Default 1000 — high enough that legitimate recursion has headroom but low enough
    /// that real infinite loops don't blow the stack.
    /// </summary>
    public int MaxDepth { get; init; } = 1000;

    /// <summary>
    /// Pushes a new <see cref="Call.@this"/>, sets it as the AsyncLocal Current, appends to
    /// <c>Caller.Children</c>, and enforces cycle detection (MaxDepth + ContainsGoal).
    /// The returned Call IS <see cref="IAsyncDisposable"/> — use <c>await using</c> for
    /// automatic Pop.
    /// </summary>
    /// <param name="action">The action being dispatched.</param>
    /// <param name="variables">Variables instance for diff capture (when Flags.Diff is on).</param>
    /// <param name="cause">Optional async-cause link (recovery dispatch, event publish).</param>
    public Call.@this Push(ActionEntity action, Variables.@this? variables = null, Call.@this? cause = null)
    {
        var caller = _current.Value;

        // Cycle detection — depth and same-goal recurrence both trip CallStackOverflowException.
        if (caller != null)
        {
            int depth = 0;
            var node = caller;
            while (node != null) { depth++; node = node.Caller; }
            if (depth >= MaxDepth)
                throw new CallStackOverflowException(MaxDepth);
        }

        var goalName = action.Step?.Goal?.Name;
        if (!string.IsNullOrEmpty(goalName) && ContainsGoal(goalName))
            throw new CallStackOverflowException(MaxDepth);

        var call = new Call.@this(action, caller, cause, this, Flags, caller, variables ?? Variables);

        caller?.Children.Add(call);

        // FIFO eviction when history is on and the cap would be exceeded. Eviction targets
        // the caller's Children, not the global tree — same-level retention only.
        if (Flags.History && caller != null && caller.Children.Count > Flags.MaxFrames)
            caller.Children.RemoveAt(0);

        if (_root == null) _root = call;
        _current.Value = call;
        return call;
    }

    /// <summary>
    /// Restores AsyncLocal Current to <paramref name="previous"/> if <paramref name="leaving"/>
    /// is still the active value. Called by <see cref="Call.@this.DisposeAsync"/>.
    /// </summary>
    internal void RestoreCurrent(Call.@this leaving, Call.@this? previous)
    {
        if (ReferenceEquals(_current.Value, leaving))
            _current.Value = previous;
    }

    /// <summary>
    /// True if any Call in the synchronous Caller chain belongs to <paramref name="goalName"/>.
    /// Case-insensitive. Used by Push to enforce no-direct/no-indirect goal cycles.
    /// </summary>
    public bool ContainsGoal(string goalName)
    {
        var node = _current.Value;
        while (node != null)
        {
            var name = node.Action.Step?.Goal?.Name ?? node.Action.Module;
            if (string.Equals(name, goalName, StringComparison.OrdinalIgnoreCase))
                return true;
            node = node.Caller;
        }
        return false;
    }
}
