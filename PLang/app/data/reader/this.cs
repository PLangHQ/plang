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
    // Bytes entry — a caller with the value's own verbatim bytes (a shape-layer host read
    // handing a param subtree) reads a Data without knowing this reader's format. json (the
    // format) is owned here, not leaked to the caller. The bytes are this reader's own encoding.
    public Data Read(byte[] raw, global::app.type.reader.ReadContext ctx)
    {
        var utf8 = new System.Text.Json.Utf8JsonReader(raw);
        utf8.Read();
        var reader = new global::app.channel.serializer.json.Reader(utf8);
        return Read(ref reader, ctx);
    }

    public Data Read(ref global::app.channel.serializer.json.Reader reader,
        global::app.type.reader.ReadContext ctx)
    {
        string name = "";
        global::app.type.@this? typeRef = null;
        Properties? properties = null;
        string? deferredRaw = null;    // a value captured for lazy materialization
        string? deferredFormat = null; // the serializer the captured value needs (string→value, else json)
        global::app.type.item.@this? born = null;   // a value read eagerly (goal.call — born, not deferred)

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
                        : ctx.Context.App.Type.Reader.Reader("type", null, ctx.Context)
                              .Read(ref reader, null, ctx)
                          as global::app.type.@this;
                    break;
                case "value":
                    // goal.call is read EAGERLY through its reader (a build/Peek consumer expects
                    // the GoalCall, not a deferred source) — the reader builds its own options
                    // for the nested Data params, so the data reader stays options-free.
                    if (typeRef is { IsNull: false } && typeRef.Name == "goal.call")
                    {
                        born = ctx.Context.App.Type.Reader.Reader("goal.call", null, ctx.Context)
                            .Read(ref reader, null, ctx);
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
            // typeRef.Create's first branch defers wire-raw to a source carrying typeRef's own
            // template flag (the build stamped it on an authored %ref% value) — NOT inferred from
            // content. A value with a %x% the build did not mark is plain data, never resolved.
            var data = new Data("", typeRef.Create(deferredRaw, ctx.Context, deferredFormat), context: ctx.Context);
            data.Name = name;
            if (properties != null) data.Properties = properties;
            return data;
        }
        // No value slot — a typed absence under its declared type. Born WITH the read
        // context: a typed null is still a value construction (type.Build), so it must
        // carry context like every other Data built on this read path.
        var typedNull = new Data(name, (object?)null, typeRef, context: ctx.Context);
        if (properties != null) typedNull.Properties = properties;
        return typedNull;
    }

    // Properties read off the inner reader into the metadata bag. Property values are
    // EAGERLY parsed (not deferred as sources): they are small metadata leaves — there is
    // no large payload to skip, so a lazy source buys nothing. The one real laziness, a
    // %ref% in a property value, is handled by the async read door (Properties.Value).
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
