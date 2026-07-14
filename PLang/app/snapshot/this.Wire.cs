namespace app.snapshot;

/// <summary>
/// Snapshot — its own wire serialization. The snapshot owns how it crosses the
/// disk boundary; nothing above it needs to know its layout. "Sections
/// self-serialize": each <see cref="ISnapshot"/> subsystem serializes the slice
/// it captured (it alone knows the concrete CLR type behind each <c>object?</c>
/// entry). The per-section dispatch order mirrors <see cref="global::app.@this.Restore"/>.
///
/// <para>Non-signing Store view (<see cref="global::app.channel.serializer.plang.@this.SnapshotOptions"/>):
/// a snapshot is internal in-process state replayed into the same actor, not an
/// actor-boundary crossing.</para>
/// </summary>
public sealed partial class @this
{
    /// <summary>
    /// Serializes this snapshot to its wire string. The snapshot rides as the
    /// Value of a <c>snapshot</c>-typed Data through the channel serializer, so
    /// its own leaf-serializer (<see cref="serializer.Default"/>) renders it
    /// format-agnostically — the snapshot never names a format. Non-signing Store
    /// view (a snapshot is internal in-process state, not an actor-boundary
    /// crossing). A context is required so the renderer + type registry are in
    /// scope.
    /// </summary>
    public async System.Threading.Tasks.Task<string> Serialize(global::app.actor.context.@this context)
    {
        var serializer = new global::app.channel.serializer.plang.@this(context);
        // The snapshot writes ITSELF via Output (base → Write → serializer.Default, section by section)
        // — bare (no Data envelope, unsigned: it's internal in-process state). The read (Create) is
        // envelope-tolerant, so the bare {…sections…} round-trips.
        using var ms = new System.IO.MemoryStream();
        await serializer.SerializeItemAsync(ms, this, global::app.View.Store);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// The born-with-context creation door (<c>Data.Value&lt;snapshot&gt;</c> dispatches here).
    ///
    /// <para>RESTORE IS DEFERRED to the ISnapshot redesign. Snapshot capture + write are live
    /// (the write is format-independent — it drives <see cref="global::app.channel.serializer.IWriter"/>).
    /// The read half — rebuilding each typed section from the wire — will move onto an
    /// <c>IReader</c> (symmetric with the writer), replacing the STJ read cursor that was torn
    /// out with the last <c>JsonConverter</c>/<c>JsonSerializerOptions</c>. Until that lands,
    /// reading a snapshot back throws.</para>
    /// </summary>
    public static @this? Create(global::app.type.item.@this value, global::app.data.@this data)
    {
        if (value is @this self) return self;
        throw new System.NotSupportedException(
            "Snapshot restore is deferred to the ISnapshot redesign — the read cursor rebuilds " +
            "on IReader (symmetric with the IWriter write side) there. See Documentation/v0.2/todos.md.");
    }
}
