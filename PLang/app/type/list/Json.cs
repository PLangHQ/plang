using System.Text.Json;
using System.Text.Json.Serialization;
using Data = global::app.data.@this;

namespace app.type.list;

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
            JsonSerializer.Serialize(writer, item.Value, options);
        writer.WriteEndArray();
    }

    public override @this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        var element = JsonElement.ParseValue(ref reader);
        var built = new @this();
        if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray())
                built.Add(new Data("", Data.UnwrapJsonElement(item)));
        return built;
    }
}
