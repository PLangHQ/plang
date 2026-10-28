using app.error;

namespace app.call.list;

public sealed partial class @this
{
    // The frame the run stopped in, made with its callers as a snapshot wrote them — held until the run goes on from it
    // (Resume), which enters them and makes it current in its own flow.
    private call.@this? _resumed;

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
    /// was new). The change lands on every frame capturing that store's diffs.
    /// </summary>
    internal void Record(global::app.type.item.variable.list.@this store, string name, object? before)
    {
        foreach (var frame in _diffing) frame.Record(store, name, before);
    }

    /// <summary>For the write that follows in this flow, the run stands at <paramref name="frame"/>, with its callers:
    /// an error's callback is written standing where the error happened, whose frames have ended by the time its
    /// handler takes it. Set in the flow of its awaited caller, it is undone when that caller returns.</summary>
    internal void Stand(call.@this frame) => _current.Value = frame;

    /// <summary>Why this stack's run can't be saved now, or null when it can: a running task's first frame sits among its
    /// caller's children but goes on in its own stack, which no resume would run again.</summary>
    internal global::app.error.@this? Unsaved
    {
        get
        {
            for (var frame = _current.Value; frame != null; frame = frame.Caller)
                if (frame.Children.Any(child => !child.Ended && !child.Of(this)))
                    return new global::app.error.@this(
                        "a snapshot is not saved while a task runs: its frames go on in the task's own stack, which nothing would run again",
                        "SnapshotWhileTaskRuns", 409);
            return null;
        }
    }

    /// <summary>The outermost frame of the run a snapshot held, as it was read back: what a resume run on its own is a
    /// task of. Null when it held no call.</summary>
    internal call.@this? Resumed => _resumed?.Chain[^1];

    /// <summary>The frames a snapshot held taken in as this stack's, from the root to the frame the run stopped in, under
    /// this stack's setting, read once: entered under the frame the root was read under, as any call's are, held until
    /// the run goes on (<see cref="Resume"/>). Nothing held, or held and entered already, nothing is entered.</summary>
    internal async System.Threading.Tasks.Task Take()
    {
        // a frame entered has begun under a setting
        if (_resumed is not { } stopped || stopped.Setting != null) return;
        var setting = await Setting();
        for (var at = stopped.Chain.Count - 1; at >= 0; at--) Enter(stopped.Chain[at], setting);
    }

    /// <summary>
    /// Goes on from the frame a snapshot held, in this flow, outward. Its frames are entered first, from the root to the
    /// frame the run stopped in, under the restored app's setting, read once: the first under the frame current now (a
    /// resume inside a run is a call, so its frames go under the resuming action's). Then each frame ends — current goes
    /// back to its caller — and what is left of its run runs there (<c>Rest</c>): the stopped action again and the rest
    /// of its step, the rest of each goal, the action after each call — up to the frame the restored tree went under. An
    /// answer that leaves its goal (a return, an ask) runs no rest: it leaves each frame in turn, and a goal's frame ends
    /// a return made in it, as the goal's own start does, so its caller goes on. The answer is the run's last, as a goal
    /// call's is.
    /// </summary>
    internal async System.Threading.Tasks.Task<global::app.data.@this> Resume(global::app.actor.context.@this context)
    {
        await Take();
        if (_resumed is not { } current)
            return context.Error(new ServiceError("there is no frame to go on from: the snapshot held no call", "NoFrameToResume", 400));
        _resumed = null;
        // the frame the restored tree went under: the root's caller, as it was read
        var under = current.Chain[^1].Caller;
        // made current in this flow: entering them in an awaited take is undone when it returns
        _current.Value = current;

        var result = context.Ok();
        for (var frame = current; frame != null && !ReferenceEquals(frame, under); frame = frame.Caller)
        {
            // ending a frame is synchronous, so the current it puts back stays in this flow
            await frame.DisposeAsync();
            if (!result.ShouldExit()) result = await frame.Rest(context, ReferenceEquals(frame, current), result);
            result = frame.Leave(result);
        }
        return result;
    }

}
