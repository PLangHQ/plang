using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.type.text;

/// <summary>
/// Plain-JSON view of a <see cref="@this"/> — bare string. Mirrors dict/list's
/// raw-STJ projection so a materialized value re-serializes value-only (not the
/// wrapper's C# surface). NOT the application/plang wire (that routes through
/// Data.Normalize + json.Writer, which has its own bare case for this wrapper).
/// </summary>
public sealed class Json : JsonConverter<@this>
{
    public override void Write(Utf8JsonWriter writer, @this value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);

    public override @this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        => new(reader.TokenType == JsonTokenType.Null ? "" : reader.GetString() ?? "");
}
