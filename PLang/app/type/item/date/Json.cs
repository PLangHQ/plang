using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.type.item.date;

/// <summary>Plain-JSON view — bare ISO date (<c>yyyy-MM-dd</c>). See text/Json.cs.</summary>
public sealed class Json : JsonConverter<@this>
{
    public override void Write(Utf8JsonWriter writer, @this value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());

    public override @this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        => new(System.DateOnly.Parse(reader.GetString() ?? "", System.Globalization.CultureInfo.InvariantCulture));
}
