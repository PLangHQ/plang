namespace app.module.snapshot;

/// <summary>
/// PLang's <c>- save snapshot, write to %snapshot%</c> verb: the run's state as it is now, as a value — the app and
/// every part under it, the asker's calls among them. Keeping it (a file, a db row) is the developer's own step. A run
/// resumed from it goes on from this action, so the save answers again, with a snapshot of the resumed run. Inside a
/// task it is refused (<c>SnapshotInTask</c>): a task's frames go on in the task's own stack.
/// <para>The state is taken once, by the app (written in the snapshot's own view, held as those bytes), so whatever
/// carries it later — a file, a db row — carries exactly them. It is signed by the app's own identity, and a resume takes
/// only a snapshot this app signed.</para>
/// </summary>
[Action("save", Cacheable = false)]
public partial class save : IContext
{
    public async System.Threading.Tasks.Task<data.@this<global::app.snapshot.@this>> Start()
    {
        var taken = await Context.App.Snapshot(Context);
        if (!taken.Success) return taken;

        // signed through the signer itself, so the view it names (the snapshot's, which no step gives) is the one it hashes in
        var (signer, missing) = Context.App.Code.Get<global::app.module.signing.code.ISigning>();
        if (signer == null) return global::app.data.@this<global::app.snapshot.@this>.FromError(missing!, Context);
        var signed = await signer.SignAsync(new global::app.module.signing.sign(Context) { Data = taken, View = global::app.View.Snapshot });
        // a snapshot is never kept unsigned: a sign that failed says why, whole; one that answered with no signature is
        // the signer's fault, named so
        if (!signed.Success) return global::app.data.@this<global::app.snapshot.@this>.From(signed);
        if (signed.Signature == null)
            return global::app.data.@this<global::app.snapshot.@this>.FromError(new global::app.error.@this(
                "the signer answered the snapshot with no signature", "SignerGaveNoSignature", 500), Context);
        return global::app.data.@this<global::app.snapshot.@this>.From(signed);
    }
}
