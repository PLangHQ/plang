using app.error;
using ActionEntity = app.goal.step.action.@this;
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

    // --- Capture knobs: plang-typed properties, set by the CLI convert-walk
    //     (--callstack={"timing":true} → Setting.Set(app.CallStack, dict), same as --build sets
    //     Build.Files). Defaults inline. Each toggles one tier of Call data capture:
    //       Timing   — StartedAt/CompletedAt/Duration
    //       Diff     — Variables.OnSet → Call.Diffs (scalar-only unless DeepDiff)
    //       DeepDiff — deep-clone non-scalar Before values (only meaningful with Diff)
    //       Tags     — advisory hint for exporters (Call.Tag() writes always succeed)
    //       History  — retain popped Calls in Caller.Children (FIFO-capped at MaxFrames)
    //       MaxFrames— history-on retention cap
    public @bool  Timing    { get; set; } = @bool.False;
    public @bool  Diff      { get; set; } = @bool.False;
    public @bool  DeepDiff  { get; set; } = @bool.False;
    public @bool  Tags      { get; set; } = @bool.False;
    public @bool  History   { get; set; } = @bool.False;
    public number MaxFrames { get; set; } = 1000;

    /// <summary>
    /// Run-wide accumulator of every error observed (handled or unhandled). Survives Pop.
    /// See <see cref="audit.@this"/> for thread-safety + lifecycle.
    /// </summary>
    public audit.@this Audit { get; } = new();

    /// <summary>
    /// Optional Variables source for diff capture. Set by <see cref="Push"/> via the
    /// per-call <c>variables</c> argument; the active Call subscribes to its <c>OnSet</c>
    /// when <see cref="Diff"/> is on.
    /// </summary>
    public Variables? Variables { get; internal set; }

    /// <summary>
    /// The current Call in this async context. Null when no Push has happened on this branch.
    /// </summary>
    public call.@this? Current => _current.Value;

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
    /// Default 1000 — high enough that legitimate recursion has headroom but low enough
    /// that real infinite loops don't blow the stack.
    /// </summary>
    public int MaxDepth { get; init; } = 1000;

    /// <summary>
    /// Pushes a new <see cref="call.@this"/>, sets it as the AsyncLocal Current, appends to
    /// <c>Caller.Children</c>, and enforces the depth limit (MaxDepth) — the only limit: a goal may
    /// call itself, directly or through others.
    /// The returned Call IS <see cref="IAsyncDisposable"/> — use <c>await using</c> for
    /// automatic Pop.
    /// </summary>
    /// <param name="action">The action being dispatched.</param>
    /// <param name="variables">Variables instance for diff capture (when Flags.Diff is on).</param>
    public call.@this Push(ActionEntity action, Variables? variables = null)
    {
        var caller = _current.Value;

        // the caller's depth is its own fact: one more frame past MaxDepth is the overflow
        if (caller != null && caller.Depth >= MaxDepth)
            throw new CallStackOverflowException(MaxDepth);

        var call = new call.@this(action, caller, this, caller, variables ?? Variables);

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

}
