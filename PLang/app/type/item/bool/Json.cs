using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.type.item.@bool;

/// <summary>Plain-JSON view — bare <c>true</c>/<c>false</c>. See text/Json.cs.</summary>
public sealed class Json : JsonConverter<@this>
{
    public override void Write(Utf8JsonWriter writer, @this value, JsonSerializerOptions options)
        => writer.WriteBooleanValue(value.Value);

    public override @this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetBoolean());
}
