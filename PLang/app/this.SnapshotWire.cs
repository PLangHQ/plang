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
    /// Parses a JSON string back into a snapshot tree through the value door — the
    /// json rides as a <c>Data</c> in the given actor context and is asked for
    /// <c>snapshot</c>, dispatching to <see cref="global::app.snapshot.@this.Create"/>
    /// (born-with-context). The result is the same in-memory shape <see cref="Snapshot()"/>
    /// produces, so <see cref="Restore"/> consumes it unchanged.
    /// </summary>
    public async Task<global::app.snapshot.@this> SnapshotFromWire(string json, global::app.actor.context.@this context)
    {
        var wire = new global::app.data.@this("", json, context: context);
        return await wire.Value<global::app.snapshot.@this>()
            ?? throw new System.InvalidOperationException("Snapshot could not be rebuilt from wire JSON");
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
