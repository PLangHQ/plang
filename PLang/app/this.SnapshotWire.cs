namespace app;

/// <summary>
/// App — snapshot↔disk wire concern. <see cref="Snapshot(actor.context.@this)"/> builds the
/// in-memory tree; this pair persists it round-trippably so a captured failure
/// can be replayed deterministically with no live LLM (durable execution).
///
/// <para>
/// The snapshot writes itself: its entries are plang values (a section is an entry whose value is
/// a snapshot), each writing its own wire shape; the snapshot's registered reader reads them back.
/// </para>
/// </summary>
public sealed partial class @this
{
    /// <summary>
    /// Serializes a snapshot tree to a JSON string. Thin wrapper — the snapshot
    /// owns its wire shape (<see cref="global::app.snapshot.@this.Serialize"/>) and
    /// carries the actor context it was captured under, which the path converter uses.
    /// </summary>
    public System.Threading.Tasks.Task<string> SnapshotToWire(global::app.snapshot.@this s)
        => s.Serialize(s.Context);

    /// <summary>
    /// Parses a JSON string back into a snapshot tree. The json rides as a <c>snapshot</c>-TYPED
    /// Data, so asking for its value materializes it through the snapshot's own registered reader —
    /// the mirror of <see cref="global::app.snapshot.@this.Serialize"/>. Declaring the type is what
    /// routes the read to the reader instead of asking a converter to turn text into a snapshot.
    /// The result is the same in-memory shape <see cref="Snapshot(actor.context.@this)"/> produces, so
    /// <see cref="Restore"/> consumes it unchanged.
    /// </summary>
    public async Task<global::app.data.@this<global::app.snapshot.@this>> SnapshotFromWire(string json, global::app.actor.context.@this context)
    {
        // A still-encoded slice born holding the format that reads it — plang's own, the mirror of
        // Serialize. A structured payload rides here rather than as a bare source: a source decodes a
        // scalar off its own token and has no document to walk.
        var snapshotType = context.App.type.list["snapshot"];
        var slice = new global::app.type.item.wire.@this(
            json, snapshotType, (global::app.type.item.wire.kind.plang.@this)context.App.type.list["wire"].kind["plang"]!);
        var wire = new global::app.data.@this("", slice, snapshotType, context: context);
        // the snapshot, or the reason its wire didn't read — whole, it is the one fact that names the cause
        return await wire.Value<global::app.snapshot.@this>() is { } snapshot
            ? context.Ok<global::app.snapshot.@this>(snapshot)
            : context.Error<global::app.snapshot.@this>(wire.Error
                ?? new global::app.error.Error("the snapshot's wire read as nothing", "SnapshotUnreadable", 400));
    }

    /// <summary>
    /// Load-and-resume from a stored snapshot's wire JSON: parse → <see cref="Restore"/>
    /// → walk the captured CallStack chain and re-enter the failing step
    /// (<see cref="global::app.snapshot.@this.Resume"/>) — deterministically, with
    /// no live LLM. The caller reads the <c>.snapshot</c> file through the path
    /// verbs and hands the string here (System.IO stays out of the engine).
    /// </summary>
    public async Task<global::app.data.@this> ResumeFromWire(string json, global::app.actor.context.@this context)
    {
        var snapshot = await SnapshotFromWire(json, context);
        return snapshot.Success ? await (await snapshot.Value())!.Resume(context) : snapshot;
    }
}
