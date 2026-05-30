using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.type;

/// <summary>
/// JSON wire/`.pr` shape for <see cref="@this"/>. The entity owns a controlled,
/// compact JSON form, distinct from its in-memory catalog navigation surface.
///
/// <para>Read accepts BOTH forms STJ-default cannot express in one type:
/// a bare string (<c>"text"</c> / <c>"image/jpeg"</c> — the slash splits in
/// <see cref="@this.Create"/>) and the dict (<c>{name, kind?, strict?}</c>).
/// Write always emits the dict, omitting kind/strict when not informative so
/// primitives stay compact.</para>
///
/// <para>Format-pluggability (protobuf, etc.) for a <em>type value crossing a
/// channel</em> lives in <c>serializer/Default.cs</c> (the IWriter renderer) —
/// this is the JSON-specific door (the `.pr` is JSON on disk).</para>
/// </summary>
public sealed class json : JsonConverter<@this?>
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
            string? name = null, kind = null;
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
                    case "name": name = reader.TokenType == JsonTokenType.Null ? null : reader.GetString(); break;
                    case "kind": kind = reader.TokenType == JsonTokenType.Null ? null : reader.GetString(); break;
                    case "strict":
                        if (reader.TokenType == JsonTokenType.True) strict = true;
                        else if (reader.TokenType == JsonTokenType.String
                                 && bool.TryParse(reader.GetString(), out var b)) strict = b;
                        break;
                    default: reader.Skip(); break;
                }
            }
            return string.IsNullOrWhiteSpace(name) ? null : @this.Create(name!, kind, strict);
        }

        throw new JsonException($"Expected string or object for app.type.@this, got {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, @this? value, JsonSerializerOptions options)
    {
        if (value == null) { writer.WriteNullValue(); return; }
        writer.WriteStartObject();
        writer.WriteString("name", value.Name);
        if (!string.IsNullOrEmpty(value.Kind)) writer.WriteString("kind", value.Kind);
        if (value.Strict) writer.WriteBoolean("strict", true);
        writer.WriteEndObject();
    }
}
