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

    /// <summary>The one read — the Data is born with the read's context.</summary>
    public Data Read(ref global::app.channel.serializer.json.Reader reader,
        global::app.type.reader.ReadContext ctx)
    {
        var born = ctx.Context;
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
                    // Loud, never a guess — plang is strongly typed. The build's retry hands this
                    // message to the LLM, so it says what every row must carry.
                    if (typeRef is not { IsNull: false }) throw new UntypedValueException(name, reader.RawValue());
                    value = typeRef.Read(ref reader, ctx);
                    break;
                case "properties":
                    properties = Properties.Read(ref reader.Inner);
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
            // resolves %refs% against data.Context. CleanName handles the name.
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
}

/// <summary>A value row that carries no type — plang is strongly typed, so this is loud, never a
/// guess. The build's retry hands this message to the LLM, so it says what every row must carry.</summary>
public sealed class UntypedValueException : JsonException
{
    public UntypedValueException(string name, byte[] raw) : base(Message(name, raw)) { }

    private static new string Message(string name, byte[] raw)
    {
        var preview = System.Text.Encoding.UTF8.GetString(raw);
        if (preview.Length > 120) preview = preview[..120] + "…";
        var slot = string.IsNullOrEmpty(name) ? "(unnamed)" : name;
        return $"property '{slot}' has no type. Every property carries its type — "
            + $"{{\"name\": \"{slot}\", \"type\": {{\"name\": \"text\"}}, \"value\": …}}, "
            + $"the type as the menu declares it ('item' where it is open). Value was: {preview}";
    }
}
