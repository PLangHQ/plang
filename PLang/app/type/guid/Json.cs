using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.type.guid;

/// <summary>Plain-JSON view — bare canonical string. See text/Json.cs.</summary>
public sealed class Json : JsonConverter<@this>
{
    public override void Write(Utf8JsonWriter writer, @this value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString("D", System.Globalization.CultureInfo.InvariantCulture));

    public override @this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        => new(System.Guid.Parse(reader.GetString() ?? ""));
}
