namespace PLang.Tests;

/// <summary>
/// A save and a start through the doors production takes: the snapshot an app's save made, and an app started from one
/// (the start <c>resume</c> makes before it re-enters the frames).
/// </summary>
public static class SnapshotSaveExtensions
{
    /// <summary>A snapshot that holds nothing, as one read back from <c>{}</c>, born in <paramref name="context"/>.</summary>
    public static global::app.snapshot.@this EmptySnapshot(this global::app.actor.context.@this context) => new(null, context);

    /// <summary>The snapshot <paramref name="saved"/> holds — the bytes the app took, opened, as a resume opens them; a
    /// refused save throws its error (a test of the refusal asks the Data).</summary>
    public static async Task<global::app.snapshot.@this> Saved(this Task<global::app.data.@this<global::app.snapshot.@this>> saved)
    {
        var taken = await saved;
        if (!taken.Success) throw new global::app.error.AppException(taken.Error!);
        return await taken.Value() as global::app.snapshot.@this ?? throw new global::app.error.AppException(taken.Error!);
    }

    /// <summary>Goes on with <paramref name="snapshot"/> through the door production takes, <c>resume %snapshot%</c>:
    /// signed by the app's signer as the app signs one it takes (an edited snapshot is signed again), then resumed. The
    /// app holds the identity its signer signs as (<c>TestIdentity()</c> beside the no-crypto signer).</summary>
    public static async Task<global::app.data.@this> Resumed(this global::app.snapshot.@this snapshot,
        global::app.actor.context.@this context, bool parallel = false)
    {
        var (signer, missing) = context.App.Code.Get<global::app.module.signing.code.ISigning>();
        if (signer == null) return context.Error(missing!);
        var signed = await signer.SignAsync(new global::app.module.signing.sign(context)
        {
            Data = new global::app.data.@this("snapshot", snapshot, context: context),
            View = global::app.View.Snapshot,
        });
        if (!signed.Success) return signed;
        await context.Variable.Set("resumed", signed);
        return await (await context.Action(parallel ? "snapshot.resume(Snapshot=%resumed%, Parallel=true)" : "snapshot.resume(Snapshot=%resumed%)")).Start(context);
    }

    /// <summary>Reads <paramref name="snapshot"/> onto <paramref name="app"/>, each object reading itself back, then takes
    /// in again the code the run took in — what a resume does before the run goes on; what can't come back throws its
    /// error.</summary>
    public static async Task Restore(this global::app.@this app, global::app.snapshot.@this snapshot,
        global::app.actor.context.@this context)
    {
        var started = await snapshot.Start(app, context);
        if (!started.Success) throw new global::app.error.AppException(started.Error!);
    }

    /// <summary>What reading <paramref name="snapshot"/> onto <paramref name="app"/> answers, as a resume reads it before
    /// the run goes on: each object reading itself back, then the code the run took in taken in again.</summary>
    public static async Task<global::app.data.@this> Start(this global::app.snapshot.@this snapshot, global::app.@this app,
        global::app.actor.context.@this context)
    {
        var started = await snapshot.Read(app, context);
        if (!started.Success) return started;
        // the frames a resume enters before the run goes on
        foreach (var actor in app.actor.list.Items()) await actor.Context.call.Take();
        return await app.Code.Take(context);
    }
}
