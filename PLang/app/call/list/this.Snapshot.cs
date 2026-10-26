using app.error;

namespace app.call.list;

public sealed partial class @this : global::app.snapshot.ISnapshot
{
    // The frame the run stopped in, put back by a snapshot's start, and the frame the restored tree went under (the
    // resuming action's; none in a fresh app) — held until the run goes on from it (Resume), which makes it current in
    // its own flow: a current set inside the awaited start would be undone when it returns.
    private (call.@this Stopped, call.@this? Under)? _resumed;

    // CallStack-level diff stream — every change of the store it was opened on, while a diff scope
    // (EnableDiffStream) is open. Independent of per-Call Diffs (which require Flags.Diff at Push
    // time). Errors.Push uses this so handler-time mutations during an error scope land
    // on the stream even when Flags.Diff was off when the live Calls were pushed.
    private readonly List<Diff> _streamDiffs = new();
    private readonly object _streamLock = new();
    private global::app.type.item.variable.list.@this? _streamVariables;

    // The frames capturing their store's diffs (Diff was on when they were pushed), each until it ends.
    private readonly object _diffingLock = new();
    private call.@this[] _diffing = [];

    /// <summary>
    /// Opens the stream on <paramref name="vars"/>: from now every change of that store appends to the
    /// CallStack's own diff stream. Idempotent — opening it again while open is a no-op.
    /// </summary>
    internal void EnableDiffStream(global::app.type.item.variable.list.@this? vars)
    {
        if (vars == null || _streamVariables != null) return;
        _streamVariables = vars;
    }

    /// <summary>A frame starts capturing its store's diffs, until it ends (<see cref="Close"/>).</summary>
    internal void Open(call.@this frame)
    {
        lock (_diffingLock) _diffing = [.. _diffing, frame];
    }

    /// <summary>A frame's capture ends.</summary>
    internal void Close(call.@this frame)
    {
        lock (_diffingLock) _diffing = Array.FindAll(_diffing, f => !ReferenceEquals(f, frame));
    }

    /// <summary>
    /// The store <paramref name="store"/> changed: <paramref name="name"/> held <paramref name="before"/> (null: it
    /// was new). The change lands on the open diff stream when it is that store's, and on every frame capturing
    /// that store's diffs — the history a snapshot reverse-applies to stand where an error was thrown.
    /// </summary>
    internal void Record(global::app.type.item.variable.list.@this store, string name, object? before)
    {
        if (ReferenceEquals(_streamVariables, store))
            lock (_streamLock) _streamDiffs.Add(new Diff(name, before, DateTimeOffset.UtcNow));
        foreach (var frame in _diffing) frame.Record(store, name, before);
    }

    /// <summary>
    /// Opens a diff-capture scope. For as long as it is open <see cref="Diff"/> is on and every
    /// variable mutation lands on the CallStack's own stream, so <c>Variables.SnapshotAt</c> can
    /// reverse-apply post-throw mutations to project back to throw-time state. Disposing closes the
    /// scope (Diff falls back to the setting) and tears the subscriptions down.
    ///
    /// The stream, the flag and the subscriptions are all CallStack state, so the scope over them
    /// is CallStack's to hand out — error handling opens one, it does not run one. Pay-per-error:
    /// nothing outside such a scope pays for the capture.
    /// </summary>
    public IDisposable DiffScope(global::app.type.item.variable.list.@this? variables)
    {
        Interlocked.Increment(ref _diffScopes);
        EnableDiffStream(variables);
        return new Diffing(this);
    }

    private sealed class Diffing : IDisposable
    {
        private readonly @this _stack;
        private bool _disposed;

        public Diffing(@this stack) => _stack = stack;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _stack.DisableDiffStream();
            Interlocked.Decrement(ref _stack._diffScopes);
        }
    }

    /// <summary>Closes the stream opened by <see cref="EnableDiffStream"/>.</summary>
    internal void DisableDiffStream() => _streamVariables = null;

    /// <summary>
    /// This stack as it stood at a throw point — the chain an error carried (<c>CallFrames</c>, the failing frame
    /// first). By handler time the live stack has unwound past the failing action, so an error callback stores the
    /// chain from the throw; starting it starts this stack.
    /// </summary>
    public global::app.snapshot.ISnapshot At(IReadOnlyList<call.@this> chain) => new Thrown(this, chain);

    private sealed class Thrown(@this stack, IReadOnlyList<call.@this> chain) : global::app.snapshot.ISnapshot
    {
        // the chain nested as it stood: each frame's one child the frame it had called
        public global::app.data.@this Store(global::app.snapshot.@this snapshot, global::app.actor.context.@this context)
        {
            if (chain.Count == 0) return context.Ok();
            var called = new Dictionary<call.@this, call.@this>(ReferenceEqualityComparer.Instance);
            for (int i = 1; i < chain.Count; i++) called[chain[i]] = chain[i - 1];
            return stack.Store(snapshot, chain[^1], chain[0],
                frame => called.TryGetValue(frame, out var child) ? [child] : [], context);
        }

        public System.Threading.Tasks.Task<global::app.data.@this> Start(global::app.snapshot.@this snapshot, global::app.actor.context.@this context)
            => ((global::app.snapshot.ISnapshot)stack).Start(snapshot, context);
    }

    // The stack as a part: its tree as it lives now, from the root — each frame's children those still running.
    global::app.data.@this global::app.snapshot.ISnapshot.Store(global::app.snapshot.@this snapshot, global::app.actor.context.@this context)
        => _current.Value is { } current
            ? Store(snapshot, current.Chain[^1], current, frame => frame.Children.Where(child => !child.Ended), context)
            : context.Ok();

    /// <summary>
    /// Writes the tree from <paramref name="root"/> as the stack lives: <c>root</c>, each frame writing itself and the
    /// frames it called (<paramref name="children"/>) nested under it in <c>children</c> — a frame's caller is the frame
    /// it sits under — and <c>current</c>, the id of the frame the run is in. A running task's first frame sits among
    /// its caller's children but goes on in its own stack, which no resume would run again, so the save is refused.
    /// </summary>
    private global::app.data.@this Store(global::app.snapshot.@this snapshot, call.@this root, call.@this current,
        Func<call.@this, IEnumerable<call.@this>> children, global::app.actor.context.@this context)
    {
        var node = new global::app.snapshot.@this(context);
        var stored = Store(node, root, children, context);
        if (!stored.Success) return stored;
        snapshot.Entries.Set(new global::app.data.@this("root", node, context: context));
        snapshot.Entries.Set(new global::app.data.@this("current", current.Id, context: context));
        return context.Ok();
    }

    private global::app.data.@this Store(global::app.snapshot.@this node, call.@this frame,
        Func<call.@this, IEnumerable<call.@this>> children, global::app.actor.context.@this context)
    {
        var stored = frame.Store(node, context);
        if (!stored.Success) return stored;
        var nodes = new List<global::app.type.item.@this>();
        foreach (var child in children(frame))
        {
            if (!child.Of(this))
                return context.Error(new global::app.error.@this(
                    "a snapshot is not saved while a task runs: its frames go on in the task's own stack, which nothing would run again",
                    "SnapshotWhileTaskRuns", 409));
            var childNode = new global::app.snapshot.@this(context);
            stored = Store(childNode, child, children, context);
            if (!stored.Success) return stored;
            nodes.Add(childNode);
        }
        node.Write("children", new global::app.type.item.list.@this(nodes, _types[typeof(global::app.type.item.list.@this)]));
        return context.Ok();
    }

    /// <summary>
    /// Puts the stored tree back as this stack's: walking down from <c>root</c>, each frame born again under the frame it
    /// sits under, with the id it was saved with, where it stood, its own variables and the rest of its state. The root
    /// goes under the frame current now — a resume inside a run is a call, so its tree goes under the resuming action's
    /// frame — and the stored current is held, with that frame, for <see cref="Resume"/>. Each frame's goal is found by its address through <c>app.goal</c>, its
    /// hash checked against the live goal, its step and action found by index. A frame that can't come back is the answer
    /// (<c>CallbackGoalHashMismatch</c>, <see cref="CallbackGoalNotFound"/>, <see cref="CallbackActionMismatch"/>,
    /// <c>CallbackFrameIncomplete</c>).
    /// </summary>
    async System.Threading.Tasks.Task<global::app.data.@this> global::app.snapshot.ISnapshot.Start(global::app.snapshot.@this snapshot, global::app.actor.context.@this context)
    {
        try
        {
            if (snapshot.In("root", context) is not { } rootEntry) return context.Ok();
            if (await rootEntry.Value<global::app.snapshot.@this>() is not { } root) return rootEntry;
            var back = new Dictionary<string, call.@this>(StringComparer.Ordinal);
            var started = await Start(root, back, context);
            if (!started.Success) return started;
            if (snapshot.In("current", context) is { } current)
            {
                if (await current.Value<global::app.type.item.text.@this>() is not { } id) return current;
                _resumed = back.GetValueOrDefault(id.ToString()) is { } stopped ? (stopped, _current.Value) : null;
            }
            return context.Ok();
        }
        catch (AppException ex)
        {
            // a frame's constructor has no answer to give, so what it can't place it throws
            return context.Error(ex.Error);
        }
    }

    // One stored frame born again under the current frame (none: the root): its async half (call.Find) while its caller
    // is current, then its kind's own Create, the type it was stored as; then it is current while the frames under it
    // come back, each by id into back. The current set here is undone when this returns.
    private async System.Threading.Tasks.Task<global::app.data.@this> Start(global::app.snapshot.@this node,
        Dictionary<string, call.@this> back, global::app.actor.context.@this context)
    {
        var found = await call.@this.Find(node, this, context);
        if (!found.Success) return found;
        if (node.Entries.Get("type", context) is not { } stored) return context.Error(call.@this.Incomplete("type"));
        if (await stored.Value<global::app.type.@this>() is not { } kind) return stored;
        var born = new global::app.data.@this("frame", context: context);
        if (kind.Make(node, born) is not call.@this frame)
            return born.Success ? context.Error(call.@this.Incomplete("type")) : born;

        // in as a push enters: among its caller's children (the root when there is none), and current
        Enter(frame);
        back[frame.Id.ToString()] = frame;

        var children = await node.Read<global::app.snapshot.@this>("children", context);
        if (await children.Value() is not { } nodes) return children;
        foreach (var child in nodes.Items())
        {
            var started = await Start(child, back, context);
            if (!started.Success) return started;
        }
        return context.Ok();
    }


    /// <summary>
    /// Goes on from the frame a snapshot's start put back as current, in this flow (which makes it current), outward:
    /// each frame ends — current goes back to its caller — and what is left of its run runs there (<c>Rest</c>): the
    /// stopped action again and the rest of its step, the rest of each goal, the action after each call — up to the frame
    /// the restored tree went under, where it stops: a resume inside a run is a call, and the resuming goal goes on
    /// itself, once. A result that exits the goal (an ask) is the answer; otherwise the restored run's last answer is.
    /// </summary>
    internal async System.Threading.Tasks.Task<global::app.data.@this> Resume(global::app.actor.context.@this context)
    {
        if (_resumed is not var (current, under))
            return context.Error(new ServiceError("there is no frame to go on from: the snapshot held no call", "NoFrameToResume", 400));
        _resumed = null;
        _current.Value = current;

        var result = context.Ok();
        for (var frame = current; frame != null && !ReferenceEquals(frame, under); frame = frame.Caller)
        {
            // ending a frame is synchronous, so the current it puts back stays in this flow
            await frame.DisposeAsync();
            result = await frame.Rest(context, ReferenceEquals(frame, current));
            if (result.ShouldExit()) return result;
        }
        return result;
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

        var seen = new HashSet<call.@this>(ReferenceEqualityComparer.Instance);

        // Per-Call Diffs (populated when Flags.Diff was on at Push time).
        for (var node = _current.Value; node != null; node = node.Caller)
            foreach (var diff in DiffsOf(node, t))
                yield return diff;

        if (_root != null)
            foreach (var diff in WalkTree(_root, t, seen))
                yield return diff;
    }

    private static IEnumerable<Diff> WalkTree(call.@this node, DateTimeOffset t, HashSet<call.@this> seen)
    {
        if (!seen.Add(node))
            yield break;
        foreach (var diff in DiffsOf(node, t))
            yield return diff;
        foreach (var child in node.Children)
            foreach (var diff in WalkTree(child, t, seen))
                yield return diff;
    }

    private static IEnumerable<Diff> DiffsOf(call.@this call, DateTimeOffset t)
    {
        if (call.Diffs == null) yield break;
        foreach (var d in call.Diffs)
            if (d.At > t) yield return d;
    }
}
