namespace PLang.Tests;

/// <summary>
/// A save and a start through the doors production takes: the snapshot an app's save made, and an app started from one
/// (the start <c>resume</c> makes before it re-enters the frames).
/// </summary>
public static class SnapshotSaveExtensions
{
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

    /// <summary>Starts <paramref name="app"/> and every part under it from <paramref name="snapshot"/>; a part that
    /// can't come back throws its error.</summary>
    public static async Task Restore(this global::app.@this app, global::app.snapshot.@this snapshot,
        global::app.actor.context.@this context)
    {
        var started = await snapshot.Start(app, context);
        if (!started.Success) throw new global::app.error.AppException(started.Error!);
    }
}
