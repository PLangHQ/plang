namespace app;

/// <summary>
/// App — snapshot↔disk wire concern. <see cref="Snapshot()"/> builds the
/// in-memory tree; this pair persists it round-trippably so a captured failure
/// can be replayed deterministically with no live LLM (durable execution).
///
/// <para>
/// The per-section dispatch mirrors <see cref="Restore"/> exactly — same names,
/// same order. Each section's owning subsystem serializes its own subtree
/// ("sections self-serialize"): the snapshot tree stores entries as
/// <c>object?</c>, so only the subsystem knows the concrete type to round-trip.
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
    /// The result is the same in-memory shape <see cref="Snapshot()"/> produces, so
    /// <see cref="Restore"/> consumes it unchanged.
    /// </summary>
    public async Task<global::app.snapshot.@this> SnapshotFromWire(string json, global::app.actor.context.@this context)
    {
        // A still-encoded slice born holding the serializer that reads it — the exact mirror of
        // Serialize, which wrote through the same one. A structured payload rides here rather than
        // as a bare source: a source decodes a scalar off its own token and has no document to walk.
        var snapshotType = new global::app.type.@this("snapshot");
        var slice = new global::app.type.item.wire.@this(
            json, snapshotType, context, new global::app.channel.serializer.plang.@this(context));
        var wire = new global::app.data.@this("", slice, snapshotType, context: context);
        var snapshot = await wire.Value<global::app.snapshot.@this>();
        // Carry the real reason. A decline here is a materialization failure with its own message;
        // replacing it with "could not be rebuilt" hides the one fact that identifies the cause.
        return snapshot ?? throw new System.InvalidOperationException(
            $"Snapshot could not be rebuilt from wire JSON — {wire.Error?.Message ?? "the value door declined without an error"}");
    }

    /// <summary>
    /// Load-and-resume from a stored snapshot's wire JSON: parse → <see cref="Restore"/>
    /// → walk the captured CallStack chain and re-enter the failing step
    /// (<see cref="global::app.snapshot.@this.Resume"/>) — deterministically, with
    /// no live LLM. The caller reads the <c>.snapshot</c> file through the path
    /// verbs and hands the string here (System.IO stays out of the engine).
    /// </summary>
    public async Task<global::app.data.@this> ResumeFromWire(string json, global::app.actor.context.@this context)
        => await (await SnapshotFromWire(json, context)).Resume(context);
}
