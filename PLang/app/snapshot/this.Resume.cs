namespace app.snapshot;

/// <summary>
/// Resume — goes on with the run this snapshot saved, in an app of its own: a sub app made by the asker's actor (layers
/// over the asker's app), every part started into it, then the run going on in its actor of the asker's role, from the
/// frame it stopped in, outward (<see cref="global::app.call.list.@this"/>'s resume). The asker's variables, settings
/// and calls are never touched. What the run answers is the answer, taken whole into the asker's context; a run that
/// fails answers its error, holding its callback, taken while its run still stood. The app ends when its run ends.
/// Reached only through the resume action, which first checks the snapshot is this app's own.
/// </summary>
public sealed partial class @this
{
    /// <summary>Resumes this snapshot for <paramref name="context"/>: to its end, or, <paramref name="parallel"/>, as a
    /// task of the asker's actor answered at once, its goal the restored run's outermost. What can't be restored is the
    /// answer, before any task starts.</summary>
    internal async Task<data.@this> Resume(actor.context.@this context, data.@this<global::app.type.item.parallel.@this>? parallel)
    {
        global::app.@this app;
        // an app nested past the deepest one may be (a run that resumes itself without end) is refused at its making
        try { app = new global::app.@this(context.App.filesystem.root.Absolute, context.Actor); }
        catch (global::app.error.AppException refused) { return context.Error(refused.Error); }
        var run = context.Actor.Role.In(app.actor.list).Context;
        var started = await Start(app, run);
        if (!started.Success)
        {
            await app.DisposeAsync();
            return started.Copy(context);
        }
        // a snapshot that held no call has no goal to run as a task: going on answers that there is no frame
        if (parallel == null || !await parallel.ToBooleanAsync() || run.call.Root?.Goal is not { } goal)
            return await On(app, run, context);
        return context.Ok(context.Actor.Task.Start(goal, async token =>
        {
            await using var stops = token.Register(app.RequestShutdown);
            return await On(app, run, context);
        }));
    }

    // The run goes on in app, as run's actor, to its end, and app ends with it.
    private async Task<data.@this> On(global::app.@this app, actor.context.@this run, actor.context.@this context)
    {
        await using (app)
        {
            var result = await run.call.Resume(run);
            if (result.Error is { } error) await error.Callback(context);
            return result.Copy(context);
        }
    }
}
