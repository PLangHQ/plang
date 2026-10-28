namespace app;

public sealed partial class @this
{
    /// <summary>
    /// The run's state as a snapshot: the app written in the snapshot view, each object writing its own tagged members,
    /// taken once and answered as those bytes, so the run going on never changes what was taken, and whatever carries it
    /// (a save, an ask's pending state, an error's callback, a file) carries exactly them. It is born signed by the app's
    /// own identity: a resume goes on only from a snapshot this app signed, so one taken here is never told from one read
    /// in by anything but its signature. A save inside a task is refused: a task's context is no member of anything the
    /// app writes, so its variables and its frames would be missing from what was saved; so is one while a task runs, whose
    /// frames go on in the task's own stack, which nothing would run again.
    /// </summary>
    public async System.Threading.Tasks.Task<data.@this<snapshot.@this>> Snapshot(actor.context.@this context)
    {
        if (!ReferenceEquals(context, context.Actor.Context))
            return global::app.data.@this<snapshot.@this>.FromError(new global::app.error.@this(
                "a snapshot is saved from the run's own context, not from inside a task: a task's variables and frames are reached by nothing a snapshot saves",
                "SnapshotInTask", 400), context);
        foreach (var actor in this.actor.list.Items())
            if (actor.Context.call.Unsaved is { } error) return global::app.data.@this<snapshot.@this>.FromError(error, context);
        var taken = context.Ok(await global::app.type.item.wire.@this.Of(new snapshot.@this(this, context), global::app.View.Snapshot, context));

        // signed through the signer itself, so the view it names (the snapshot's, which no step gives) is the one it hashes in
        var (signer, missing) = Code.Get<global::app.module.signing.code.ISigning>();
        if (signer == null) return global::app.data.@this<snapshot.@this>.FromError(missing!, context);
        var signed = await signer.SignAsync(new global::app.module.signing.sign(context) { Data = taken, View = global::app.View.Snapshot });
        // a snapshot is never unsigned: a sign that failed says why, whole; one that answered with no signature is the
        // signer's fault, named so
        if (!signed.Success) return global::app.data.@this<snapshot.@this>.From(signed);
        if (signed.Signature == null)
            return global::app.data.@this<snapshot.@this>.FromError(new global::app.error.@this(
                "the signer answered the snapshot with no signature", "SignerGaveNoSignature", 500), context);
        return global::app.data.@this<snapshot.@this>.From(signed);
    }

    /// <summary>
    /// <paramref name="error"/>'s callback: the run as it stands when its handler takes it, going on from where the error
    /// happened. For this one write the call stands in the error's frame, with its callers (by the time a handler asks,
    /// the frames the error happened in have ended); the variables are as they are now.
    /// </summary>
    public async System.Threading.Tasks.Task<data.@this<snapshot.@this>> Snapshot(global::app.error.@this error, actor.context.@this context)
    {
        if (error.call is { } call) context.call.Stand(call);
        return await Snapshot(context);
    }
}
