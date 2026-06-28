using System.Collections.Generic;
using System.Text.Json;
using Data = global::app.data.@this;
using Properties = global::app.data.Properties;

namespace app.data.reader;

/// <summary>
/// The <c>@schema:data</c> reader — reads a Data wire object <c>{name, type, value,
/// properties}</c> into a Data. The read counterpart of the Data writer: a value defers to a
/// lazy <c>source</c> (captured raw off the reader, materialized through its type's reader on
/// first touch), so this only assembles the envelope. Stateless — the per-read actor context +
/// authored template ride on <see cref="ReadContext"/>, mirroring the type readers.
/// </summary>
public sealed class @this : global::app.data.schema.ISchemaReader
{
    public string Schema => "data";

    // Reads through the IReader abstraction (json.Reader). The reader carries the owned bytes
    // (entry path) so a structured value slices raw off the buffer with no DOM; the goal.call
    // TEMP dips to the inner reader for its STJ JsonConverter — the rest is format-agnostic.
    public Data Read(ref global::app.channel.serializer.json.Reader reader,
        global::app.type.reader.ReadContext ctx, JsonSerializerOptions options)
    {
        string name = "";
        global::app.type.@this? typeRef = null;
        Properties? properties = null;
        string? deferredRaw = null;    // a value captured for lazy materialization
        string? deferredFormat = null; // the serializer the captured value needs (string→value, else json)
        global::app.type.item.@this? born = null;   // a value born inline (goal.call TEMP)

        reader.BeginObject();
        while (reader.NextName(out var key))
        {
            switch (key.ToLowerInvariant())
            {
                case "name":
                    name = reader.Null() ? "" : reader.String();
                    break;
                case "type":
                    // The type is the structured entity {name, kind?, strict?}. A bare string
                    // form (type:"string") is the OLD shape — invalid; throw so a stale .pr
                    // surfaces loudly. The entity reads through its own reader (the `type`
                    // reader, like any other value) — context stamped there.
                    if (reader.Peek() == global::app.channel.serializer.TokenKind.String)
                        throw new JsonException(
                            $"invalid .pr schema: 'type' must be an object {{name, ...}}, not the bare string "
                            + $"\"{reader.String()}\" (value slot '{(string.IsNullOrEmpty(name) ? "(unnamed)" : name)}').");
                    typeRef = reader.Null()
                        ? null
                        : ctx.Context.App.Type.Readers.Reader("type", null, ctx.Context)
                              .Read(ref reader, null, ctx)
                          as global::app.type.@this;
                    break;
                case "value":
                    // TEMP: goal.call has no reader yet — born it inline off the inner reader.
                    // Remove once it streams like any other structured value.
                    if (typeRef is { IsNull: false } && typeRef.Name == "goal.call")
                    {
                        born = JsonSerializer.Deserialize<global::app.goal.GoalCall>(ref reader.Inner, options);
                    }
                    else if (typeRef is not { IsNull: false })
                    {
                        var preview = System.Text.Encoding.UTF8.GetString(reader.RawValue());
                        if (preview.Length > 120) preview = preview[..120] + "…";
                        throw new JsonException(
                            $"invalid .pr schema: value slot '{(string.IsNullOrEmpty(name) ? "(unnamed)" : name)}' "
                            + $"has no declared type. Value was: {preview}");
                    }
                    else
                    {
                        // The value rides as its raw bytes — off the reader, no DOM (scalar off
                        // the token, structured sliced from the owned buffer) — materializing
                        // lazily. A string is content (→ value/text), incl a full-match %ref%
                        // which its source resolves on read (gated on the authored template);
                        // any other token is json.
                        deferredFormat = reader.Peek() == global::app.channel.serializer.TokenKind.String
                            ? global::app.channel.serializer.Text.Mime : "application/plang";
                        deferredRaw = System.Text.Encoding.UTF8.GetString(reader.RawValue());
                    }
                    break;
                case "properties":
                    properties = ReadPropertiesObject(ref reader.Inner);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }
        reader.EndObject();

        if (born != null)
        {
            var d = new Data(name, born);
            if (properties != null) d.Properties = properties;
            return d;
        }
        if (deferredRaw != null && typeRef != null)
        {
            // Lazy value slot: rides as its raw source form, materializes on first touch
            // through the read door (the serializer the raw needs + the authored template).
            var lazy = Data.FromRaw(deferredRaw, typeRef, ctx.Context, format: deferredFormat, template: ctx.Template);
            lazy.Name = name;
            if (properties != null) lazy.Properties = properties;
            return lazy;
        }
        // No value slot — a typed absence under its declared type.
        var typedNull = new Data(name, (object?)null, typeRef);
        if (properties != null) typedNull.Properties = properties;
        return typedNull;
    }

    // Properties read off the inner reader into the object?-valued bag. Making each value a
    // lazy source (plan line 35) is blocked by the sync Properties[key] getter; an IReader
    // rewrite (plan line 50) mis-advanced the signature-wrapped path — both logged in
    // stage-final-cleanup #7. Kept on Utf8JsonReader for now.
    private static Properties ReadPropertiesObject(ref Utf8JsonReader reader)
    {
        var props = new Properties();
        if (reader.TokenType == JsonTokenType.Null) return props;
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("properties field must be a JSON object");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) return props;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected property name inside properties object");
            var key = reader.GetString()!;
            reader.Read();
            props[key] = ReadPropertyPrimitive(ref reader);
        }
        throw new JsonException("Unterminated properties object");
    }

    private static object? ReadPropertyPrimitive(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null: return null;
            case JsonTokenType.String: return reader.GetString();
            case JsonTokenType.True: return true;
            case JsonTokenType.False: return false;
            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var l)) return l;
                // A bare decimal-point literal defaults to double (universal language
                // convention); decimal is opt-in via `as number/decimal`.
                return reader.GetDouble();
            case JsonTokenType.StartArray:
            {
                var list = new List<object?>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    list.Add(ReadPropertyPrimitive(ref reader));
                return list;
            }
            case JsonTokenType.StartObject:
            {
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    var key = reader.GetString()!;
                    reader.Read();
                    dict[key] = ReadPropertyPrimitive(ref reader);
                }
                return dict;
            }
            default:
                throw new JsonException($"Unexpected token in property value: {reader.TokenType}");
        }
    }
}
