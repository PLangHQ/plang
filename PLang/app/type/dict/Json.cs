using System.Text.Json;
using System.Text.Json.Serialization;
using Data = global::app.data.@this;

namespace app.type.dict;

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

    public override @this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        var element = JsonElement.ParseValue(ref reader);
        // Store raw, type on read — the json entry parse builds a native dict
        // whose slots hold raw scalars / native sub-containers, not a Data per
        // entry. Empty/non-object falls back to an empty dict.
        return global::app.type.item.serializer.json.Parse(element) as @this ?? new @this();
    }
}
