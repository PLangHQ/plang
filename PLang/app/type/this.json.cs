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
            string? name = null, kind = null, template = null;
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
                    case "template": template = reader.TokenType == JsonTokenType.Null ? null : reader.GetString(); break;
                    case "strict":
                        if (reader.TokenType == JsonTokenType.True) strict = true;
                        else if (reader.TokenType == JsonTokenType.String
                                 && bool.TryParse(reader.GetString(), out var b)) strict = b;
                        break;
                    default: reader.Skip(); break;
                }
            }
            return string.IsNullOrWhiteSpace(name) ? null : @this.Create(name!, kind, strict, template: template);
        }

        // HACK (minimal, "just get the build working"): the build LLM occasionally
        // emits a parameter `type` as a JSON ARRAY (e.g. a list-typed param rendered as
        // ["text"] / [{"name":"text"}] / ["list","text"]) instead of the required string
        // or {name,kind,strict} object. That is an LLM/prompt bug — `type` must never be
        // an array. Throwing here turns one occasional bad field into a whole-build crash
        // (DeserializationFailed at BuildStep/Start Compile). Tolerate it instead: take the
        // first string/object element as the type, ignore the rest, fall back to null
        // (runtime infers). TODO(coder): fix at the prompt/schema layer (CompileUser.llm
        // "Type reference" — forbid array `type`, teach list types as a single name like
        // "list<text>") and then DELETE this branch. See
        // .bot/type-kind-strict/builder/v2/baseline-findings.md.
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            @this? fromArray = null;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (fromArray == null && reader.TokenType == JsonTokenType.String)
                {
                    var s = reader.GetString();
                    if (!string.IsNullOrEmpty(s)) fromArray = @this.Create(s);
                }
                else if (fromArray == null && reader.TokenType == JsonTokenType.StartObject)
                {
                    fromArray = Read(ref reader, typeToConvert, options);
                }
                else
                {
                    reader.Skip();
                }
            }
            return fromArray;
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
        if (!string.IsNullOrEmpty(value.Template)) writer.WriteString("template", value.Template);
        writer.WriteEndObject();
    }
}
