using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.type.item.datetime;

/// <summary>Plain-JSON view — bare ISO instant. See text/Json.cs.</summary>
public sealed class Json : JsonConverter<@this>
{
    public override void Write(Utf8JsonWriter writer, @this value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);

    public override @this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetDateTimeOffset());
}
