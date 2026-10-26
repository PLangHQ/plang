namespace app.snapshot;

/// <summary>
/// Resume — the path the channel and host take to go on with a run a snapshot saved: the app and every part under it
/// start from this snapshot (the walk), the asker's call stack among them, put back as it lived; then the run goes on
/// from the frame it stopped in, outward (<see cref="global::app.call.list.@this"/>'s resume).
/// </summary>
public sealed partial class @this
{
    public async Task<data.@this> Resume(actor.context.@this context)
    {
        var started = await Start(context.App, context);
        if (!started.Success) return started;
        return await context.call.Resume(context);
    }
}
