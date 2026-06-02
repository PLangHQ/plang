using System.Text.Json;
using System.Text.Json.Nodes;

namespace app.snapshot;

/// <summary>
/// The wire cursor a subsystem reads and writes through when its snapshot
/// subtree crosses the disk boundary. Wraps one JSON object node (the
/// subsystem's section) plus the shared <see cref="JsonSerializerOptions"/>
/// carrying the Data <c>Wire</c>, the path converter, and the
/// <see cref="global::app.error.ErrorWire"/> polymorphic IError converter.
///
/// <para>
/// "Sections self-serialize": the snapshot tree stores entries as
/// <c>object?</c>, so a central serializer can't recover their CLR type on
/// read. Each <see cref="ISnapshot"/> subsystem instead names the concrete
/// <c>T</c> it owns — <c>Put&lt;List&lt;data.@this&gt;&gt;("variables", …)</c> on
/// write, <c>Get&lt;List&lt;data.@this&gt;&gt;("variables")</c> on read — and the
/// io never inspects the value graph itself.
/// </para>
/// </summary>
public sealed class Io
{
    /// <summary>The JSON object backing this section. Mutated on write, read on read.</summary>
    public JsonObject Node { get; }

    /// <summary>Shared options — same instance threads through every nested section.</summary>
    public JsonSerializerOptions Options { get; }

    public Io(JsonObject node, JsonSerializerOptions options)
    {
        Node = node;
        Options = options;
    }

    /// <summary>Serializes <paramref name="value"/> as <typeparamref name="T"/> under <paramref name="key"/>.</summary>
    public void Put<T>(string key, T value)
        => Node[key] = JsonSerializer.SerializeToNode(value, Options);

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

    /// <summary>
    /// Opens (write) a nested subsection — used by subsystems whose snapshot
    /// shape nests (Errors → "trail", CallStack → each frame). Creates the child
    /// object and returns a cursor over it sharing the same options.
    /// </summary>
    public Io PutSection(string key)
    {
        var child = new JsonObject();
        Node[key] = child;
        return new Io(child, Options);
    }

    /// <summary>Reads a nested subsection, or null when absent / not an object.</summary>
    public Io? GetSection(string key)
        => Node.TryGetPropertyValue(key, out var n) && n is JsonObject o
            ? new Io(o, Options)
            : null;

    /// <summary>
    /// Opens (write) a nested array of subsections under <paramref name="key"/>.
    /// The returned factory appends a fresh object cursor on each call — used by
    /// a subsystem holding a list of element-owned subtrees (CallStack → frames),
    /// where each element serializes itself into its own cursor.
    /// </summary>
    public Func<Io> PutSectionList(string key)
    {
        var arr = new JsonArray();
        Node[key] = arr;
        return () =>
        {
            var child = new JsonObject();
            arr.Add(child);
            return new Io(child, Options);
        };
    }

    /// <summary>
    /// Enumerates (read) the object cursors of the array under <paramref name="key"/>.
    /// Empty when the key is absent or not an array — symmetric to <see cref="PutSectionList"/>.
    /// </summary>
    public IEnumerable<Io> GetSectionList(string key)
    {
        if (Node.TryGetPropertyValue(key, out var n) && n is JsonArray arr)
            foreach (var item in arr)
                if (item is JsonObject o)
                    yield return new Io(o, Options);
    }
}
