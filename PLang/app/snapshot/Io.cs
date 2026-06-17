using System.Text.Json;
using System.Text.Json.Nodes;

namespace app.snapshot;

/// <summary>
/// The read cursor a subsystem rebuilds its snapshot subtree through. Wraps one
/// parsed JSON object node (the subsystem's section) plus the shared
/// <see cref="JsonSerializerOptions"/> carrying the Data <c>Wire</c>, the path
/// converter, and the <see cref="global::app.error.ErrorWire"/> polymorphic
/// IError converter.
///
/// <para>
/// Write is not here — the snapshot serializes itself through its leaf-serializer
/// (<see cref="serializer.Default"/>) via <see cref="global::app.channel.serializer.IWriter"/>,
/// format-agnostically. Read stays explicit: the parsed tree is untyped, so each
/// <see cref="ISnapshot"/> subsystem names the concrete <c>T</c> it owns —
/// <c>Get&lt;List&lt;data.@this&gt;&gt;("variables")</c> — to rebuild its section.
/// </para>
/// </summary>
public sealed class Io
{
    /// <summary>The parsed JSON object backing this section.</summary>
    public JsonObject Node { get; }

    /// <summary>Shared options — same instance threads through every nested section.</summary>
    public JsonSerializerOptions Options { get; }

    public Io(JsonObject node, JsonSerializerOptions options)
    {
        Node = node;
        Options = options;
    }

    /// <summary>
    /// Deserializes the entry at <paramref name="key"/> as <typeparamref name="T"/>.
    /// Returns <c>default</c> when the key is absent or JSON-null — callers needing
    /// presence use <see cref="Has"/>.
    /// </summary>
    public T? Get<T>(string key)
        => Node.TryGetPropertyValue(key, out var n) && n != null
            ? n.Deserialize<T>(Options)
            : default;

    /// <summary>True if an entry with this key is present on the section.</summary>
    public bool Has(string key) => Node.ContainsKey(key);

    /// <summary>Reads a nested subsection, or null when absent / not an object.</summary>
    public Io? GetSection(string key)
        => Node.TryGetPropertyValue(key, out var n) && n is JsonObject o
            ? new Io(o, Options)
            : null;

    /// <summary>
    /// Enumerates (read) the object cursors of the array under <paramref name="key"/>.
    /// Empty when the key is absent or not an array.
    /// </summary>
    public IEnumerable<Io> GetSectionList(string key)
    {
        if (Node.TryGetPropertyValue(key, out var n) && n is JsonArray arr)
            foreach (var item in arr)
                if (item is JsonObject o)
                    yield return new Io(Unwrap(o), Options);
    }

    // A structured list element rides as a Data record ({"@schema":"data", type,
    // value:{…}}) — the section's own fields live under "value". Peel that one
    // envelope so the reader sees the fields directly; a bare object passes through.
    private static JsonObject Unwrap(JsonObject o)
        => o.TryGetPropertyValue("@schema", out var s) && s?.GetValue<string>() == "data"
           && o.TryGetPropertyValue("value", out var v) && v is JsonObject inner
            ? inner
            : o;
}
