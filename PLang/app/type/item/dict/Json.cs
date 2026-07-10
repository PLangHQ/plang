using System.Text.Json;
using System.Text.Json.Serialization;
using Data = global::app.data.@this;

namespace app.type.item.dict;

/// <summary>
/// Plain-JSON view of a <see cref="@this"/> — the "value as JSON" projection used
/// by the <c>application/json</c> channel serializer (<c>app.channel.serializer.Json</c>)
/// and any raw STJ path. A dict emits as a JSON object keyed by entry name, with
/// each entry's bare value written through the same options (so a nested dict / list
/// recurses correctly).
///
/// <para>This is NOT the wire (<c>application/plang</c>) path — that routes through
/// <c>Data.Normalize</c> + the IWriter and never reflects. Without this converter,
/// raw STJ would reflect the dict's C# surface (Entries → Data → …) and cycle.</para>
/// </summary>
public sealed class Json : JsonConverter<@this>
{
    public override void Write(Utf8JsonWriter writer, @this value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var entry in value.Entries)
        {
            writer.WritePropertyName(entry.Name);
            // Serialize against the RUNTIME type so each value's own
            // [JsonConverter] fires (text→string, list→array, choice→enum).
            // Compile-time `object` makes STJ reflect the item base
            // (Cacheable/Prior/Template/IsLeaf) instead of the value.
            var v = entry.Peek();
            JsonSerializer.Serialize(writer, v, v?.GetType() ?? typeof(object), options);
        }
        writer.WriteEndObject();
    }

    // Write-only projection. A dict is never READ back through STJ — every read
    // flows through the context-carrying path (the wire Reader for .pr/channels,
    // json.Parse for a string typed `as json`). An STJ read here would have no
    // context to born the dict's entries with, so it is refused loudly rather than
    // silently producing a context-less value.
    public override @this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        => throw new System.NotSupportedException(
            "A dict is not read through STJ — read it through the context-carrying Reader (wire) or json.Parse.");
}
