namespace app.module.snapshot;

/// <summary>
/// PLang's <c>- save snapshot, write to %snapshot%</c> verb: the run's state as it is now, as a value — the app and
/// every part under it, the asker's calls among them. Keeping it (a file, a db row) is the developer's own step. A run
/// resumed from it goes on from this action, so the save answers again, with a snapshot of the resumed run. Inside a
/// task it is refused (<c>SnapshotInTask</c>): a task's frames go on in the task's own stack.
/// <para>The state is taken once, here: written in the snapshot's own view (no secret in it) and held as those bytes,
/// so whatever carries it later — a file, a db row — carries exactly them. It is signed by the app's own identity, and
/// a resume takes only a snapshot this app signed.</para>
/// </summary>
[Action("save", Cacheable = false)]
public partial class save : IContext
{
    public async System.Threading.Tasks.Task<data.@this<global::app.snapshot.@this>> Start()
    {
        var saved = Context.App.Snapshot(Context);
        if (await saved.Value() is not { } snapshot) return saved;

        using var bytes = new System.IO.MemoryStream();
        await using (var utf8 = new System.Text.Json.Utf8JsonWriter(bytes))
            await snapshot.Output(new global::app.type.item.wire.kind.plang.Writer(utf8, global::app.View.Snapshot, Context));
        var types = Context.App.type.list;
        var plang = (global::app.type.item.wire.kind.plang.@this)types["wire"].kind["plang"]!;
        var taken = Context.Ok(new global::app.type.item.wire.@this(
            System.Text.Encoding.UTF8.GetString(bytes.ToArray()), types[typeof(global::app.snapshot.@this)], plang));

        // signed through the signer itself, so the view it names (the snapshot's, which no step gives) is the one it hashes in
        var (signer, missing) = Context.App.Code.Get<global::app.module.signing.code.ISigning>();
        if (signer == null) return global::app.data.@this<global::app.snapshot.@this>.FromError(missing!, Context);
        var signed = await signer.SignAsync(new global::app.module.signing.sign(Context) { Data = taken, View = global::app.View.Snapshot });
        // a snapshot that should be signed and couldn't be is the save's failure, never one kept unsigned
        return signed.Signature == null
            ? global::app.data.@this<global::app.snapshot.@this>.FromError(signed.Error ?? new global::app.error.@this(
                "the snapshot could not be signed", "SnapshotUnsigned", 500), Context)
            : global::app.data.@this<global::app.snapshot.@this>.From(signed);
    }
}
