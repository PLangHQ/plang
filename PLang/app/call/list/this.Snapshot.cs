using app.error;

namespace app.call.list;

public sealed partial class @this : global::app.snapshot.ISnapshot
{
    // The frame the run stopped in, put back by a snapshot's start, and the frame the restored tree went under (the
    // resuming action's; none in a fresh app) — held until the run goes on from it (Resume), which makes it current in
    // its own flow: a current set inside the awaited start would be undone when it returns.
    private (call.@this Stopped, call.@this? Under)? _resumed;

    // CallStack-level diff stream — every change of the store it was opened on, while a diff scope (DiffScope) is open.
    // Independent of per-Call Diffs (which require Flags.Diff at Push time). Errors.Push uses this so handler-time
    // mutations during an error scope land on the stream even when Flags.Diff was off when the live Calls were pushed.
    private readonly diff.@this _stream = new();
    private global::app.type.item.variable.list.@this? _streamVariables;

    // The frames capturing their store's diffs (Diff was on when they were pushed), each until it ends.
    private readonly object _diffingLock = new();
    private call.@this[] _diffing = [];

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
        // the stream is what a throw's standing is reverse-applied from, so it keeps the value as it was: a deep copy
        if (ReferenceEquals(_streamVariables, store))
            _stream.Add(new Diff(name, before, deep: true));
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
        // the stream opens on the store asked for; opening it again while open changes nothing
        if (variables != null && _streamVariables == null) _streamVariables = variables;
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
            // the stream closes
            _stack._streamVariables = null;
            Interlocked.Decrement(ref _stack._diffScopes);
        }
    }

    /// <summary>
    /// This stack as it stood at a throw point — the chain an error carried (<c>CallFrames</c>, the failing frame
    /// first). By handler time the live stack has unwound past the failing action, so an error callback stores the
    /// chain from the throw; starting it starts this stack.
    /// </summary>
    public global::app.snapshot.ISnapshot At(IReadOnlyList<call.@this> chain) => new Thrown(this, chain);

    private sealed class Thrown(@this stack, IReadOnlyList<call.@this> chain) : global::app.snapshot.ISnapshot
    {
        // the chain as it stood, from the root to the failing frame
        public global::app.data.@this Store(global::app.snapshot.@this snapshot, global::app.actor.context.@this context)
            => chain.Count == 0 ? context.Ok() : stack.Store(snapshot, chain.Reverse(), context);

        public System.Threading.Tasks.Task<global::app.data.@this> Start(global::app.snapshot.@this snapshot, global::app.actor.context.@this context)
            => ((global::app.snapshot.ISnapshot)stack).Start(snapshot, context);
    }

    // The stack as a part: its frames as they live now, from the root to the frame the run is in.
    global::app.data.@this global::app.snapshot.ISnapshot.Store(global::app.snapshot.@this snapshot, global::app.actor.context.@this context)
        => _current.Value is { } current ? Store(snapshot, current.Chain.Reverse(), context) : context.Ok();

    /// <summary>
    /// Writes <paramref name="frames"/>, from the root to the frame the run is in, as <c>frames</c>: each frame the Data
    /// it is, its kind its type, written as itself; the last is where the run stopped. A running task's first frame sits
    /// among its caller's children but goes on in its own stack, which no resume would run again, so the save is
    /// refused.
    /// </summary>
    private global::app.data.@this Store(global::app.snapshot.@this snapshot, IEnumerable<call.@this> frames,
        global::app.actor.context.@this context)
    {
        var written = new List<global::app.data.@this>();
        foreach (var frame in frames)
        {
            if (frame.Children.Any(child => !child.Ended && !child.Of(this)))
                return context.Error(new global::app.error.@this(
                    "a snapshot is not saved while a task runs: its frames go on in the task's own stack, which nothing would run again",
                    "SnapshotWhileTaskRuns", 409));
            written.Add(new global::app.data.@this("", frame, context: context));
        }
        snapshot.Write("frames", new global::app.type.item.list.@this(written, _types[typeof(global::app.type.item.list.@this)]));
        return context.Ok();
    }

    /// <summary>
    /// Puts the stored frames back as this stack's, from the root to the frame the run stopped in: each row read in turn
    /// as its kind (the type it was stored as), made under the frame entered before it — the first under the frame
    /// current now: a resume inside a run is a call, so its frames go under the resuming action's — then entered under
    /// the restored app's setting, read once. The last is where the run stopped, held with the frame they went under for
    /// <see cref="Resume"/>. A row whose key finds nothing (a goal changed since the save, an action no longer at its
    /// place) is the answer, its error whole, and the frames entered before it end, the last first, as any call ends:
    /// current goes back to the frame they went under, and they leave its children (with history kept, they stay among
    /// them ended, history not state).
    /// </summary>
    async System.Threading.Tasks.Task<global::app.data.@this> global::app.snapshot.ISnapshot.Start(global::app.snapshot.@this snapshot, global::app.actor.context.@this context)
    {
        if (snapshot.In("frames", context) is not { } entry) return context.Ok();
        if (await entry.Value<global::app.type.item.list.@this>() is not { } rows) return entry;
        var under = _current.Value;
        var setting = await Setting();
        var entered = new List<call.@this>();
        var answer = context.Ok();
        try
        {
            // each row read only once the one before it is entered: it is made under it, its caller
            foreach (var row in rows.Items(context))
            {
                if (await row.Value<call.@this>() is not { } frame)
                {
                    answer = row.Error != null ? row : context.Error(new global::app.error.@this(
                        "the snapshot's 'frames' holds a row that reads as no frame", "SnapshotRowUnread", 400));
                    break;
                }
                entered.Add(Enter(frame, setting));
            }
        }
        catch (AppException ex)
        {
            // a frame's constructor has no answer to give, so what it can't place it throws
            answer = context.Error(ex.Error);
        }
        if (!answer.Success)
        {
            for (var at = entered.Count - 1; at >= 0; at--) await entered[at].DisposeAsync();
            return answer;
        }
        _resumed = entered.Count > 0 ? (entered[^1], under) : null;
        return answer;
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
        // CallStack-level stream (opened by a diff scope — used by error auto-flip).
        foreach (var diff in _stream.Since(t))
            yield return diff;

        // Per-Call Diffs (populated when Flags.Diff was on at Push time), each frame's once: the current chain's, then
        // the tree's under the root, which holds the chain's frames too.
        var seen = new HashSet<call.@this>(ReferenceEqualityComparer.Instance);
        for (var node = _current.Value; node != null; node = node.Caller)
            if (seen.Add(node) && node.Diffs is { } diffs)
                foreach (var diff in diffs.Since(t))
                    yield return diff;

        if (_root != null)
            foreach (var diff in _root.Since(t, seen))
                yield return diff;
    }
}
