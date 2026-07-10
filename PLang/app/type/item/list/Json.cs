using System.Text.Json;
using System.Text.Json.Serialization;
using Data = global::app.data.@this;

namespace app.type.item.list;

/// <summary>
/// Plain-JSON view of a <see cref="@this"/> — the "value as JSON" projection used
/// by the <c>application/json</c> channel and any raw STJ path. A list emits as a
/// JSON array of <em>bare</em> element values: <c>[1,"two"]</c>, signatures absent
/// (per the Stage-3 ruling — signatures ride the <c>application/plang</c> wire, not
/// plain json). Each element's bare value writes through the same options, so a
/// nested dict/list recurses through its own converter.
///
/// <para>NOT the wire path: the <c>application/plang</c> wire routes through
/// <c>Data.Normalize</c> + the IWriter, where each element self-describes as Data.
/// Without this converter, raw STJ would reflect each element's <c>Data</c> surface
/// into junk — the same failure that gave <c>dict</c> its converter.</para>
/// </summary>
public sealed class Json : JsonConverter<@this>
{
    public override void Write(Utf8JsonWriter writer, @this value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value.Items)
        {
            // Runtime type so each element's own [JsonConverter] fires —
            // compile-time `object` reflects the item base instead.
            var v = item.Peek();
            JsonSerializer.Serialize(writer, v, v?.GetType() ?? typeof(object), options);
        }
        writer.WriteEndArray();
    }

    // Write-only projection. A list is never READ back through STJ — every read
    // flows through the context-carrying path (the wire Reader for .pr/channels,
    // json.Parse for a string typed `as json`). Refused loudly rather than
    // silently producing a context-less value.
    public override @this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        => throw new System.NotSupportedException(
            "A list is not read through STJ — read it through the context-carrying Reader (wire) or json.Parse.");
}
