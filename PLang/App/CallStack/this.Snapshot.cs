using App.Errors;

namespace App.CallStack;

public sealed partial class @this : global::App.Snapshot.ISnapshot
{
    private List<RestoredFrame>? _restoredChain;

    // CallStack-level diff stream — populated by a single OnSet subscription wired by
    // EnableDiffStream. Independent of per-Call Diffs (which require Flags.Diff at Push
    // time). Errors.Push uses this so handler-time mutations during an error scope land
    // on the stream even when Flags.Diff was off when the live Calls were pushed.
    private readonly List<Diff> _streamDiffs = new();
    private readonly object _streamLock = new();
    private Action<string, object?, object?>? _streamHandler;
    private Action<string, object?>? _streamCreateHandler;
    private global::App.Variables.@this? _streamVariables;

    /// <summary>
    /// Wires OnSet + OnCreate on <paramref name="vars"/> so every variable change appends to
    /// the CallStack's own diff stream. Idempotent — calling twice on the same Variables is
    /// a no-op. Both events get captured because Variables.Set fires OnCreate for first-time
    /// names and OnSet only for replace; both are mutations the diff stream cares about.
    /// </summary>
    internal void EnableDiffStream(global::App.Variables.@this? vars)
    {
        if (vars == null || _streamHandler != null) return;
        _streamHandler = (name, before, _) =>
        {
            lock (_streamLock)
                _streamDiffs.Add(new Diff(name, before, DateTimeOffset.UtcNow));
        };
        _streamCreateHandler = (name, _) =>
        {
            lock (_streamLock)
                _streamDiffs.Add(new Diff(name, null, DateTimeOffset.UtcNow));
        };
        _streamVariables = vars;
        vars.OnSet += _streamHandler;
        vars.OnCreate += _streamCreateHandler;
    }

    /// <summary>Tears down subscriptions wired by <see cref="EnableDiffStream"/>.</summary>
    internal void DisableDiffStream()
    {
        if (_streamHandler == null || _streamVariables == null) return;
        _streamVariables.OnSet -= _streamHandler;
        if (_streamCreateHandler != null) _streamVariables.OnCreate -= _streamCreateHandler;
        _streamHandler = null;
        _streamCreateHandler = null;
        _streamVariables = null;
    }

    /// <summary>
    /// The captured-and-restored chain of frames, populated by <see cref="Restore"/>.
    /// Null on a fresh App that hasn't been restored. Not maintained automatically by
    /// live Push/Pop — restored chains are read-only positional context for callbacks.
    /// </summary>
    public IReadOnlyList<RestoredFrame>? RestoredChain => _restoredChain;

    /// <summary>
    /// The deepest frame on the resume path. On a *restored* CallStack this is the
    /// last entry of <see cref="RestoredChain"/> (the originally throwing/awaiting Call).
    /// On a *live* CallStack it's a <see cref="RestoredFrame"/> view of <see cref="Current"/>
    /// — the API is uniform whether live or restored. Null when neither side has a frame.
    /// </summary>
    public RestoredFrame? BottomFrame
    {
        get
        {
            if (_restoredChain != null && _restoredChain.Count > 0)
                return _restoredChain[^1];
            var live = _current.Value;
            return live != null ? FrameFromLive(live) : null;
        }
    }

    /// <summary>
    /// Captures the active Caller chain — outer frames first, throwing/bottom frame last.
    /// Empty when no Push has happened. Completed children are dropped (history not state).
    /// </summary>
    public void Capture(global::App.Snapshot.@this s)
    {
        var bottom = _current.Value;
        var ordered = new List<Call.@this>();
        // SnapshotChain returns [self, Caller, Caller.Caller, ...]; reverse so outer first.
        if (bottom != null)
            for (var node = bottom; node != null; node = node.Caller)
                ordered.Insert(0, node);

        var frames = new List<global::App.Snapshot.@this>();
        foreach (var call in ordered)
        {
            var frame = new global::App.Snapshot.@this();
            call.Capture(frame);
            frames.Add(frame);
        }
        s.Write("frames", frames);
    }

    /// <summary>
    /// Reconstructs the captured chain into <see cref="RestoredChain"/> on the live App's
    /// CallStack. For each captured frame, looks up the goal by PrPath in <c>app.Goals</c>,
    /// hash-matches against the live goal, then resolves the Step + Action by index.
    /// Hard-errors on goal-not-found (<see cref="CallbackGoalNotFound"/>) or hash mismatch
    /// (<see cref="CallbackGoalHashMismatch"/>). Does not mutate the live AsyncLocal Current —
    /// the resumed action is dispatched separately via App.Run from <see cref="BottomFrame"/>.
    /// </summary>
    public static void Restore(global::App.Snapshot.@this s, global::App.Actor.Context.@this ctx)
    {
        var captured = s.Read<List<global::App.Snapshot.@this>>("frames")
                       ?? new List<global::App.Snapshot.@this>();
        var restored = new List<RestoredFrame>(captured.Count);

        foreach (var frame in captured)
        {
            var goalPrPath = frame.Read<string>("goalPrPath") ?? "";
            var goalHash   = frame.Read<string>("goalHash")   ?? "";
            var stepIndex  = frame.Read<int>("stepIndex");
            var actionIndex = frame.Read<int>("actionIndex");
            var id         = frame.Read<string>("id") ?? "";

            var liveGoal = ctx.App.Goals.Get(goalPrPath);
            if (liveGoal == null)
                throw new CallbackGoalNotFound(goalPrPath);

            var liveHash = liveGoal.Hash ?? "";
            if (!string.Equals(liveHash, goalHash, StringComparison.OrdinalIgnoreCase))
                throw new CallbackGoalHashMismatch(goalPrPath, goalHash, liveHash);

            if (stepIndex < 0 || stepIndex >= liveGoal.Steps.Count)
                throw new CallbackGoalNotFound($"{goalPrPath} (stepIndex {stepIndex} out of range)");
            var liveStep = liveGoal.Steps[stepIndex];

            if (actionIndex < 0 || actionIndex >= liveStep.Actions.Count)
                throw new CallbackGoalNotFound($"{goalPrPath} (actionIndex {actionIndex} out of range at step {stepIndex})");
            var liveAction = liveStep.Actions[actionIndex];

            restored.Add(new RestoredFrame(liveAction, liveGoal, stepIndex, actionIndex, id));
        }

        ctx.App.CallStack._restoredChain = restored;
    }

    /// <summary>
    /// Variable mutation events whose timestamp is strictly later than <paramref name="t"/>.
    /// Walks the live Call tree (Current's chain plus Root's children when retained) and
    /// yields each <see cref="Diff"/> in encounter order. Order is not guaranteed to be
    /// time-sorted across siblings — last-write-wins reverse-apply is order-independent.
    /// </summary>
    public IEnumerable<Diff> EventsSince(DateTimeOffset t)
    {
        // CallStack-level stream (populated by EnableDiffStream — used by error auto-flip).
        Diff[] stream;
        lock (_streamLock) stream = _streamDiffs.ToArray();
        foreach (var d in stream)
            if (d.At > t) yield return d;

        var seen = new HashSet<string>(StringComparer.Ordinal);

        // Per-Call Diffs (populated when Flags.Diff was on at Push time).
        for (var node = _current.Value; node != null; node = node.Caller)
            foreach (var diff in DiffsOf(node, t))
                yield return diff;

        if (_root != null)
            foreach (var diff in WalkTree(_root, t, seen))
                yield return diff;
    }

    private static IEnumerable<Diff> WalkTree(Call.@this node, DateTimeOffset t, HashSet<string> seen)
    {
        if (!seen.Add(node.Id))
            yield break;
        foreach (var diff in DiffsOf(node, t))
            yield return diff;
        foreach (var child in node.Children)
            foreach (var diff in WalkTree(child, t, seen))
                yield return diff;
    }

    private static IEnumerable<Diff> DiffsOf(Call.@this call, DateTimeOffset t)
    {
        if (call.Diffs == null) yield break;
        foreach (var d in call.Diffs)
            if (d.At > t) yield return d;
    }

    private static RestoredFrame FrameFromLive(Call.@this call)
    {
        var step = call.Action.Step;
        var goal = step?.Goal;
        var stepIndex = step?.Index ?? -1;
        var actionIndex = -1;
        if (step?.Actions != null)
        {
            for (int i = 0; i < step.Actions.Count; i++)
                if (ReferenceEquals(step.Actions[i], call.Action))
                {
                    actionIndex = i;
                    break;
                }
        }
        // goal can be null in tests with hand-built actions; surface a synthetic empty Goal.
        return new RestoredFrame(call.Action, goal!, stepIndex, actionIndex, call.Id);
    }
}
