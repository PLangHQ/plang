using System.Text.Json;
using System.Text.Json.Nodes;

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
    public string Serialize(global::app.actor.context.@this context)
    {
        var serializer = new global::app.channel.serializer.plang.@this(context);
        var d = new global::app.data.@this<@this>("", this, new global::app.type.@this("snapshot"))
        {
            Context = context,
        };
        return JsonSerializer.Serialize(d, serializer.SnapshotOptions);
    }

    /// <summary>
    /// Reconstructs a snapshot tree from its wire string. The result is the same
    /// in-memory shape <see cref="global::app.@this.Snapshot"/> produces, so
    /// <see cref="global::app.@this.Restore"/> consumes it unchanged. The wire is
    /// the Data envelope <c>{name, type, value:{…sections…}}</c>; the sections
    /// object is read back per-section (the owner's type knowledge rebuilds each).
    /// </summary>
    public static @this Deserialize(string json, global::app.actor.context.@this? context = null)
    {
        var opts = WireOptions(context);
        var parsed = JsonNode.Parse(json)?.AsObject()
            ?? throw new JsonException("Snapshot wire root is not a JSON object");
        // Envelope-tolerant: a Data envelope nests the sections under "value".
        var root = parsed["value"] is JsonObject value && (parsed.ContainsKey("type") || parsed.ContainsKey("name"))
            ? value
            : parsed;
        var s = new @this();

        void Load(string name, System.Action<Io, @this> read)
        {
            if (root[name] is not JsonObject node) return;
            read(new Io(node, opts), s.Section(name));
        }

        Load("Variables", global::app.variable.list.@this.Read);
        Load("Errors",    global::app.error.list.@this.Read);
        Load("Providers", global::app.module.code.@this.Read);
        Load("Statics",   global::app.Statics.@this.Read);
        Load("Build",     global::app.module.builder.@this.Read);
        Load("Testing",   global::app.tester.@this.Read);
        Load("CallStack", global::app.callstack.@this.Read);

        return s;
    }

    /// <summary>
    /// The conversion seam <c>Data.As&lt;snapshot&gt;</c> reaches through the type
    /// registry — a string-shaped value (the wire JSON read off disk) rebuilds
    /// into the snapshot object. Kind is unused. Context-less: uses the
    /// fallback serializer recipe (the wire carries no actor-bound state that
    /// needs a live context to rehydrate).
    /// </summary>
    public static object? FromWire(string raw, string? kind)
        => string.IsNullOrEmpty(raw) ? null : Deserialize(raw);

    private static JsonSerializerOptions WireOptions(global::app.actor.context.@this? context)
        => context != null
            ? new global::app.channel.serializer.plang.@this(context).SnapshotOptions
            : global::app.channel.serializer.plang.@this.ContextLessFallback.SnapshotOptions;
}
