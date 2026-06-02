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
    /// <summary>Serializes this snapshot tree to its wire JSON string.</summary>
    public string Serialize(global::app.actor.context.@this? context = null)
    {
        var opts = WireOptions(context);
        var root = new JsonObject();

        void Emit(string name, System.Action<@this, Io> write)
        {
            if (!HasSection(name)) return;
            var node = new JsonObject();
            write(Section(name), new Io(node, opts));
            root[name] = node;
        }

        Emit("Variables", global::app.variable.list.@this.Write);
        Emit("Errors",    global::app.error.list.@this.Write);
        Emit("Providers", global::app.module.code.@this.Write);
        Emit("Statics",   global::app.Statics.@this.Write);
        Emit("Build",     global::app.module.builder.@this.Write);
        Emit("Testing",   global::app.tester.@this.Write);
        Emit("CallStack", global::app.callstack.@this.Write);

        return root.ToJsonString(opts);
    }

    /// <summary>
    /// Reconstructs a snapshot tree from its wire JSON. The result is the same
    /// in-memory shape <see cref="global::app.@this.Snapshot"/> produces, so
    /// <see cref="global::app.@this.Restore"/> consumes it unchanged.
    /// </summary>
    public static @this Deserialize(string json, global::app.actor.context.@this? context = null)
    {
        var opts = WireOptions(context);
        var root = JsonNode.Parse(json)?.AsObject()
            ?? throw new JsonException("Snapshot wire root is not a JSON object");
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
