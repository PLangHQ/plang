using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.type.archive;

/// <summary>Plain-JSON view — bare base64 string (the compressed bytes). The
/// algorithm is not carried in this bare form; the self-describing
/// <c>{@schema:"archive", type, value}</c> layer shape lands with the wire
/// @schema-dispatch work. See app/type/binary/Json.cs.</summary>
public sealed class Json : JsonConverter<@this>
{
    public override void Write(Utf8JsonWriter writer, @this value, JsonSerializerOptions options)
        => writer.WriteBase64StringValue(value.Value);

    public override @this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        => new(reader.TokenType == JsonTokenType.Null ? System.Array.Empty<byte>() : reader.GetBytesFromBase64());
}
