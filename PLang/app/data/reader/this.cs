using System.Collections.Generic;
using System.Text.Json;
using Data = global::app.data.@this;
using Properties = global::app.data.Properties;

namespace app.data.reader;

/// <summary>
/// Reads a <c>@schema:data</c> wire object — <c>{name, type, value, properties}</c> — into a
/// Data. The read counterpart of the Data writer: a value defers to a lazy <c>source</c>
/// (captured raw off the reader, materialized through its type's reader on first touch), so
/// this only assembles the envelope. Holds the read's actor context + authored template (the
/// trust mode a value's <c>source</c> carries into its read). Dispatched from the Wire
/// converter once it has read the <c>@schema</c>.
/// </summary>
public sealed class @this
{
    private readonly actor.context.@this? _context;
    private readonly string? _template;

    public @this(actor.context.@this? context, string? template)
    {
        _context = context;
        _template = template;
    }

    // buffer is the OWNED bytes when the read came in through the entry path, null when
    // STJ-driven (nested). A non-null buffer lets a structured value slot be sliced raw (no
    // DOM); null falls back to DOM. Same captured bytes either way.
    public Data Read(ref Utf8JsonReader reader, JsonSerializerOptions options, byte[]? buffer)
    {
        string name = "";
        global::app.type.@this? typeRef = null;
        Properties? properties = null;
        string? deferredRaw = null;    // a scalar value captured for lazy materialization
        string? deferredFormat = null; // the serializer the captured value needs (string→value, else json)
        string? refValue = null;       // a full-match %x% value, born at EndObject when the type is known
        global::app.type.item.@this? born = null;   // set when the type read its own value off the pass

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (refValue != null && born == null
                    && typeRef is { IsNull: false } && typeRef.Name != "variable")
                {
                    // A full-match %x% declared as a real value type → a typed variable
                    // reference: it resolves at .Value() (the variable hop), then the consumer
                    // converts to T. Never read through the type's reader at load (which would
                    // parse the literal "%x%" and null it). Name-slots (type:variable),
                    // untyped refs, and content fall through unchanged.
                    born = global::app.variable.@this.Reference(refValue, _context!);
                }
                if (born != null)
                {
                    // The declared type already read its own value off the pass, born at its
                    // kind — no lift, no Build. Data is the dumb holder.
                    var d = new Data(name, born);
                    if (properties != null) d.Properties = properties;
                    return d;
                }
                if (deferredRaw != null && typeRef != null)
                {
                    // Lazy value slot: rides as its raw source form and materializes on first
                    // touch through the read door (the serializer the captured raw needs —
                    // string content → value, else json — plus the authored template).
                    var lazy = Data.FromRaw(deferredRaw, typeRef, _context, format: deferredFormat, template: _template);
                    lazy.Name = name;
                    if (properties != null) lazy.Properties = properties;
                    return lazy;
                }
                // No value slot at all — a typed absence under its declared type. (Every
                // PRESENT value set deferredRaw / born / refValue above and returned there;
                // this is only reached when the wire carried {name, type, properties} with no
                // value.)
                var typedNull = new Data(name, (object?)null, typeRef);
                if (properties != null) typedNull.Properties = properties;
                return typedNull;
            }
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected PropertyName inside app.data.@this wire shape");
            var key = reader.GetString()!;
            reader.Read();
            switch (key.ToLowerInvariant())
            {
                case "name":
                    name = reader.TokenType == JsonTokenType.Null ? "" : reader.GetString() ?? "";
                    break;
                case "type":
                    // The type is the structured entity {name, kind?, strict?}. A bare string
                    // form (type:"string", type:"text") is the OLD shape — invalid. Throw so a
                    // stale/malformed .pr surfaces loudly instead of silently mis-borning.
                    if (reader.TokenType == JsonTokenType.String)
                        throw new JsonException(
                            $"invalid .pr schema: 'type' must be an object {{name, ...}}, not the bare string "
                            + $"\"{reader.GetString()}\" (value slot '{(string.IsNullOrEmpty(name) ? "(unnamed)" : name)}').");
                    if (reader.TokenType == JsonTokenType.Null) typeRef = null;
                    else
                    {
                        typeRef = JsonSerializer.Deserialize<global::app.type.@this>(ref reader, options);
                        // The type's JsonConverter has no actor scope, so the entity is born
                        // context-less. Stamp the reader's context now — so the type can reach
                        // its registry (App.Type) to build the value.
                        if (typeRef != null) typeRef.Context = _context;
                    }
                    break;
                case "value":
                    // A full-match %x% is a VARIABLE reference, whatever its declared type.
                    // Capture it; at EndObject it borns a variable — a real-typed slot → a
                    // reference resolving at .Value(). It must NOT run the declared type's
                    // reader on the literal "%x%" (the list reader would choke on "%!data%").
                    if (reader.TokenType == JsonTokenType.String && _template != null && _context != null
                        && reader.GetString() is { } sv
                        && Data.TryFullVarMatch(sv, out _))
                    {
                        refValue = sv;
                    }
                    // TEMP: goal.call has no reader yet, so born it inline. Remove once it gets
                    // a reader (then it streams like any other structured value).
                    else if (typeRef is { IsNull: false } && typeRef.Name == "goal.call" && _context != null)
                    {
                        using var vdoc = JsonDocument.ParseValue(ref reader);
                        born = JsonSerializer.Deserialize<global::app.goal.GoalCall>(
                            vdoc.RootElement.GetRawText(), options);
                    }
                    else if (typeRef is not { IsNull: false })
                    {
                        // Invalid .pr schema — DOM only on this error path to show the value.
                        using var vdoc = JsonDocument.ParseValue(ref reader);
                        var preview = vdoc.RootElement.GetRawText();
                        if (preview.Length > 120) preview = preview[..120] + "…";
                        throw new JsonException(
                            $"invalid .pr schema: value slot '{(string.IsNullOrEmpty(name) ? "(unnamed)" : name)}' "
                            + $"has no declared type. Value was: {preview}");
                    }
                    else
                    {
                        // The value rides as its raw bytes — captured off the reader with no
                        // DOM (scalar off the token, structured sliced from the owned buffer) —
                        // and materializes lazily on first touch. A string token IS the value's
                        // own content (→ value/text); any other token is json (→ plang/json).
                        deferredFormat = reader.TokenType == JsonTokenType.String
                            ? global::app.channel.serializer.Text.Mime : "application/plang";
                        var jr = new global::app.channel.serializer.json.Reader(reader, buffer);
                        deferredRaw = System.Text.Encoding.UTF8.GetString(jr.RawValue());
                        reader = jr.Inner;
                    }
                    break;
                case "properties":
                    properties = ReadPropertiesObject(ref reader);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        throw new JsonException("Unterminated app.data.@this wire shape");
    }

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
            object? value = ReadPropertyPrimitive(ref reader);
            props[key] = value;
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
