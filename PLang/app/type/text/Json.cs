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
    // Serialize the backing THROUGH the options, not directly — a consumer's
    // registered JsonConverter<string> (e.g. Json.WithConverter) still composes
    // over the text leaf's bare-string projection.
    public override void Write(Utf8JsonWriter writer, @this value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value.Value, options);

    public override @this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        => new(reader.TokenType == JsonTokenType.Null ? "" : reader.GetString() ?? "");
}
