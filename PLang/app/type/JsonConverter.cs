using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.type;

/// <summary>
/// Wire shape for <c>app.type.@this</c>. Two valid input forms — a plain
/// string (the legacy form, `"text"` / `"image/jpeg"`; the slash splits in
/// <see cref="@this.Create"/>) and a dict (`{name, kind?, strict?}`, the
/// LLM-emitted form for structured `type` parameters). Output is always
/// the dict form, omitting fields that are null/false so the serialized
/// shape stays compact for primitives.
/// </summary>
public sealed class JsonConverter : JsonConverter<@this?>
{
    public override @this? Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            return string.IsNullOrEmpty(s) ? null : @this.Create(s);
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            string? name = null;
            string? kind = null;
            bool strict = false;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) break;
                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("Expected property name inside type object");
                var key = reader.GetString()!;
                reader.Read();
                switch (key.ToLowerInvariant())
                {
                    case "name":
                        name = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                        break;
                    case "kind":
                        kind = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                        break;
                    case "strict":
                        if (reader.TokenType == JsonTokenType.True) strict = true;
                        else if (reader.TokenType == JsonTokenType.False) strict = false;
                        else if (reader.TokenType == JsonTokenType.String
                                 && bool.TryParse(reader.GetString(), out var b)) strict = b;
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }
            if (string.IsNullOrWhiteSpace(name)) return null;
            return @this.Create(name, kind, strict);
        }

        throw new JsonException($"Expected string or object for app.type.@this, got {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, @this? value, JsonSerializerOptions options)
    {
        if (value == null) { writer.WriteNullValue(); return; }
        // Always emit the dict form so kind/strict round-trip when present.
        // Omit kind/strict when not informative so primitives stay compact.
        writer.WriteStartObject();
        writer.WriteString("name", value.Name);
        if (!string.IsNullOrEmpty(value.Kind))
            writer.WriteString("kind", value.Kind);
        if (value.Strict)
            writer.WriteBoolean("strict", true);
        writer.WriteEndObject();
    }
}
