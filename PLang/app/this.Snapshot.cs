namespace app;

public sealed partial class @this : global::app.snapshot.ISnapshot
{
    /// <summary>
    /// The run's state as a snapshot: the app and every part under it (<see cref="snapshot.@this.Store"/>),
    /// each at its place, taken once — written in the snapshot's own view (no secret in it) and answered as those bytes,
    /// so the run going on never changes what was taken, and whatever carries it (a save, an ask's pending state, an
    /// error's callback, a file) carries exactly them. It is born signed by the app's own identity: a resume goes on only
    /// from a snapshot this app signed, so one taken here is never told from one read in by anything but its signature. A
    /// save inside a task is refused: a task's context is no member of anything the walk reaches, so its variables and its
    /// frames would be missing from what was saved.
    /// </summary>
    public System.Threading.Tasks.Task<data.@this<snapshot.@this>> Snapshot(actor.context.@this context)
        => Snapshot(context, thrown: []);

    /// <summary>
    /// Throw-time snapshot for an error callback. By the time an error reaches its handler the live call stack has
    /// unwound past the failing action, so the asker's variables and call stack are written as they stood at the throw
    /// (<see cref="global::app.type.item.variable.list.@this.SnapshotAt"/>, the chain the error carried). Everything else
    /// is unchanged across handling, so it is stored live.
    /// </summary>
    public System.Threading.Tasks.Task<data.@this<snapshot.@this>> Snapshot(global::app.error.@this error, actor.context.@this context)
        => Snapshot(context, thrown: [(context.Variable, context.Variable.SnapshotAt(error)), (context.call, context.call.At(error.CallFrames))]);

    private async System.Threading.Tasks.Task<data.@this<snapshot.@this>> Snapshot(actor.context.@this context,
        (snapshot.ISnapshot Live, snapshot.ISnapshot Thrown)[] thrown)
    {
        if (!ReferenceEquals(context, context.Actor.Context))
            return global::app.data.@this<snapshot.@this>.FromError(new global::app.error.@this(
                "a snapshot is saved from the run's own context, not from inside a task: a task's variables and frames are reached by nothing a snapshot saves",
                "SnapshotInTask", 400), context);
        var snapshot = new snapshot.@this(context);
        var stored = snapshot.Store(this, context, thrown);
        if (!stored.Success) return global::app.data.@this<snapshot.@this>.FromError(stored.Error!, context);
        var taken = context.Ok(await global::app.type.item.wire.@this.Of(snapshot, global::app.View.Snapshot, context));

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

    // The app is the root part and holds nothing of its own: what a program added to it is its property, a part at
    // `property`. Its mode is not stored: how an app was started (test, build, run) is the host's choice, not the
    // program's state, so a restore leaves the app as it was started.
}
