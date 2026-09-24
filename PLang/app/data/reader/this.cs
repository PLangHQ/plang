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

    /// <summary>A program row — an action's parameter or default read off a <c>.pr</c>. The
    /// program is shared by every run, so the row holds no context; a run reads its own copy
    /// (<c>action[name]</c>). A program row holds no context; it goes when the action-property
    /// change lands (the program then holds no Data at all).</summary>
    public Data Row(byte[] raw, global::app.type.reader.ReadContext ctx)
    {
        var utf8 = new System.Text.Json.Utf8JsonReader(raw);
        utf8.Read();
        var reader = new global::app.channel.serializer.json.Reader(utf8, raw);
        return Read(ref reader, ctx, born: null);
    }

    public Data Read(ref global::app.channel.serializer.json.Reader reader,
        global::app.type.reader.ReadContext ctx)
        => Read(ref reader, ctx, born: ctx.Context);

    // The one read. `born` is the context the Data is born with — the read context for a value,
    // none for a program row.
    private Data Read(ref global::app.channel.serializer.json.Reader reader,
        global::app.type.reader.ReadContext ctx, global::app.actor.context.@this? born)
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
                    // A type whose values are STRUCTURE is read eagerly, off this stream, through
                    // its own reader. Which types those are is the TYPE's declaration (ITypeReader
                    // .IsEager), never a list of names kept here — the courier stays generic.
                    if (typeRef is { IsNull: false }
                        && ctx.Context.App.Type.Reader.Typed(typeRef.Name, null) is { IsEager: true } eager)
                        value = eager.Read(ref reader, null, ctx);
                    else if (typeRef is not { IsNull: false })
                    {
                        // Loud, never a guess — plang is strongly typed. The build's retry hands this
                        // message to the LLM, so it says what every row must carry.
                        var preview = System.Text.Encoding.UTF8.GetString(reader.RawValue());
                        if (preview.Length > 120) preview = preview[..120] + "…";
                        var slot = string.IsNullOrEmpty(name) ? "(unnamed)" : name;
                        throw new JsonException(
                            $"parameter '{slot}' has no type. Every parameter carries its type — "
                            + $"{{\"name\": \"{slot}\", \"type\": {{\"name\": \"text\"}}, \"value\": …}}, "
                            + $"the type as the menu declares it ('item' where the slot is open). Value was: {preview}");
                    }
                    else if (reader.Peek() == global::app.channel.serializer.TokenKind.String)
                    {
                        var slice = System.Text.Encoding.UTF8.GetString(reader.Slice());
                        // plang's own %var% syntax, parsed — never a guessed type: a string carrying a
                        // variable reference is born a template of its row's type.
                        if (typeRef.Template == null && global::app.type.item.text.@this.HasVariable(slice))
                            typeRef = ctx.Context.App.Type[new global::app.type.@this(typeRef.Name, typeRef.Kind?.Name, typeRef.Strict, "plang")];
                        // A SEMANTIC string — a %ref%/template (the IsVariable birth gate needs the
                        // decoded content) or a variable NAME (type.Create resolves it to its binding) —
                        // takes the content door; the kind-parse stays lazy on the content source. A
                        // literal string under any other type rides the wire (strict, byte-identical).
                        value = typeRef.Template != null
                                || ctx.Context.App.Type[typeRef.Name]?.ClrType == typeof(global::app.variable.@this)
                            ? typeRef.Create(JsonSerializer.Deserialize<string>(slice)!, ctx.Context)
                            : typeRef.Create(slice,
                                ctx.Context.Actor?.Channel.Serializers?.Transport
                                    ?? throw new JsonException(
                                        "wire capture reached before the actor channel wired its "
                                        + "transport serializer — cannot decode a .pr value slot."));
                    }
                    else
                        // EVERY other slot — string tokens included — is a wire: a VERBATIM Slice
                        // (RawValue decodes strings, so it can't serve here), with the capturing
                        // transport serializer named at the mint site (the reader stays stateless;
                        // the wire itself never knows a format name). Face validation is free — the
                        // type's own pull IS the validator on first touch (a still-quoted "23"
                        // under {number} fails at the number pull; a string under {dict} at
                        // BeginObject). The BUILD must never emit a mismatched token.
                        value = typeRef.Create(
                            System.Text.Encoding.UTF8.GetString(reader.Slice()),
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
            // One arm: goal.call (eager), a content source, or a wire — all items. A value Data is
            // born WITH the read context: source/wire materialization renders templates and
            // resolves %refs% against data.Context. A program row is born with none — a run
            // loads its own copy. CleanName handles the name.
            var d = new Data(name, value, context: born);
            if (properties != null) d.Properties = properties;
            return d;
        }
        // No value slot — a typed absence under its declared type (the absence the type's own
        // door makes for a null raw), born with the same context as a value.
        var typedNull = typeRef is { IsNull: false }
            ? new Data(name, new global::app.type.item.@null.@this(typeRef.Name, typeRef.Kind?.Name), context: born)
            : new Data(name, (object?)null, context: born);
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
