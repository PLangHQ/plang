using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.type.@null;

/// <summary>Plain-JSON view — bare <c>null</c>; reads back the singleton. See text/Json.cs.</summary>
public sealed class Json : JsonConverter<@this>
{
    public override void Write(Utf8JsonWriter writer, @this value, JsonSerializerOptions options)
        => writer.WriteNullValue();

    public override @this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Skip();
        return @this.Instance;
    }
}
