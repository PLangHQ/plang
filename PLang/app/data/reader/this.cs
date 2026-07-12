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
        // Own the buffer so a wire slot's Slice() gets a verbatim span (not a JsonDocument
        // round-trip) on this host-carrier subtree entry too.
        var reader = new global::app.channel.serializer.json.Reader(utf8, raw);
        return Read(ref reader, ctx);
    }

    public Data Read(ref global::app.channel.serializer.json.Reader reader,
        global::app.type.reader.ReadContext ctx)
    {
        string name = "";
        global::app.type.@this? typeRef = null;
        Properties? properties = null;
        // The value slot — a lazy source (content or wire) or an eagerly-read item (goal.call);
        // a source IS an item, so one local carries every arm.
        global::app.type.item.@this? value = null;

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
                        value = ctx.Context.App.Type.Reader.Reader("goal.call", null, ctx.Context)
                            .Read(ref reader, null, ctx);
                    else if (typeRef is not { IsNull: false })
                    {
                        var preview = System.Text.Encoding.UTF8.GetString(reader.RawValue());
                        if (preview.Length > 120) preview = preview[..120] + "…";
                        throw new JsonException(
                            $"invalid .pr schema: value slot '{(string.IsNullOrEmpty(name) ? "(unnamed)" : name)}' "
                            + $"has no declared type. Value was: {preview}");
                    }
                    else if (reader.Peek() == global::app.channel.serializer.TokenKind.String
                             && (typeRef.Template != null
                                 || ctx.Context.App.Type[typeRef.Name]?.ClrType == typeof(global::app.variable.@this)
                                 || (ctx.Context.App.Type.Reader.Typed(typeRef.Name, typeRef.Kind?.Name)?.StringIsContent ?? true)))
                        // The CONTENT door. A string token borns a content source when it is: a
                        // builder-authored %ref%/template (the IsVariable birth gate needs decoded
                        // content); a variable NAME (type.Create resolves it to its binding); or a
                        // string that IS this type's own content — the reader says so (text,
                        // datetime, path, csv, base64 image, the object/item apex). Raw is the
                        // DECODED content, so Peek / interpolation / events / display read the
                        // content, never a quoted document slice.
                        value = typeRef.Create(reader.String(), ctx.Context);
                    else
                        // The STRICT wire: structured/number/bool tokens, and a string under a
                        // type whose form is NOT a string (the reader said false — a mismatch). A
                        // verbatim Slice (RawValue decodes strings, so it can't serve), captured
                        // with the transport serializer at the mint (the reader stays stateless;
                        // the wire never knows a format name). The type's own pull is the validator
                        // at first touch — a still-quoted "23" under {number} fails at the number
                        // pull; a string under {dict} at BeginObject. The BUILD must never emit a
                        // mismatched token.
                        value = typeRef.Create(
                            System.Text.Encoding.UTF8.GetString(reader.Slice()), ctx.Context,
                            ctx.Context.Actor?.Channel.Serializers?.Transport
                                ?? throw new JsonException(
                                    "wire capture reached before the actor channel wired its "
                                    + "transport serializer — cannot decode a .pr value slot."));
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
        if (value != null)
        {
            // One arm: goal.call (eager), a content source, or a wire — all items. The Data is
            // born WITH the read context: source/wire materialization renders templates and
            // resolves %refs% against data.Context, so it must carry it (a null context leaves a
            // template unrendered). CleanName handles the name.
            var d = new Data(name, value, context: ctx.Context);
            if (properties != null) d.Properties = properties;
            return d;
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
