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
        // The snapshot writes ITSELF via Output — one object of entries, each writing its own value.
        // The root rides bare (no Data envelope, unsigned: internal in-process state); a nested
        // section rides as the entry that holds it, so it carries its own type on the wire.
        using var ms = new System.IO.MemoryStream();
        await serializer.SerializeItemAsync(ms, this, global::app.View.Store);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// The born-with-context creation door (<c>Data.Value&lt;snapshot&gt;</c> dispatches here).
    ///
    /// <para>An ordinary courier: a snapshot passes through, anything else declines. The wire READ
    /// is not here — it lives at the serializer boundary in <see cref="serializer.Reader"/>, where a
    /// snapshot is read as the value it is rather than converted from some other value.</para>
    /// </summary>
    public static @this? Create(global::app.type.item.@this value, global::app.data.@this data)
        => value as @this;
}
