using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.data;

/// <summary>
/// Wire converter for <c>app.data.@this</c> — the single point where the
/// canonical four-field shape <c>{name, type, value, signature}</c> is emitted
/// and parsed.
///
/// <para>
/// Sign-if-missing: each Data the converter visits during a Write walk gets
/// <see cref="@this.EnsureSigned"/> fired before emission, idempotently — a
/// Data that already carries a Signature is left alone. Forwarding chains
/// preserve provenance because inner Datas that arrived already-signed never
/// re-sign; new Datas in a freshly-wrapped outer get their own signature.
/// </para>
///
/// <para>
/// Hash carve-out: when crypto.Hash needs to canonicalize a Data D for
/// signing, the converter is told (via <see cref="MarkOuterForHash"/>) that
/// D is the "outer being signed right now." For that one Data, Write emits
/// <c>{name, type, value}</c> with <em>no</em> signature field and does NOT
/// call EnsureSigned (which would loop). All inner Datas reached through the
/// walk still go through sign-if-missing and emit their full four fields, so
/// the outer signature transitively binds inner attestations.
/// </para>
/// </summary>
public sealed class WireJsonConverter : JsonConverter<@this>
{
    // Ref-counted "outer being hashed right now" tracking. A plain HashSet
    // would lose the marker when nested Hash calls run on the same Data
    // instance (signing.verify re-hashing inner while outer Verify is mid-
    // serialise; chained crypto.Hash through a goal whose own result is
    // being signed): inner-disposal would Remove the Data the outer still
    // depended on, and the next outer Write would think it was safe to
    // EnsureSigned, causing recursion. Per-instance ref-count keeps each
    // scope's lifetime independent.
    private static readonly AsyncLocal<Dictionary<@this, int>?> _hashOuter = new();

    /// <summary>
    /// Marks <paramref name="data"/> as the "outer being hashed right now."
    /// While the returned scope is alive, the converter writes that one
    /// Data's body without invoking <see cref="@this.EnsureSigned"/> and
    /// without emitting its Signature field. Reference-counted: nested
    /// MarkOuterForHash calls on the same Data instance compose correctly.
    /// </summary>
    public static IDisposable MarkOuterForHash(@this data)
    {
        var map = _hashOuter.Value ??= new Dictionary<@this, int>(ReferenceEqualityComparer.Instance);
        map.TryGetValue(data, out var n);
        map[data] = n + 1;
        return new Scope(data);
    }

    private static bool IsHashOuter(@this data)
    {
        var map = _hashOuter.Value;
        return map != null && map.TryGetValue(data, out var n) && n > 0;
    }

    private sealed class Scope : IDisposable
    {
        private readonly @this _data;
        private bool _disposed;
        public Scope(@this data) { _data = data; }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var map = _hashOuter.Value;
            if (map == null) return;
            if (map.TryGetValue(_data, out var n))
            {
                if (n <= 1) map.Remove(_data);
                else map[_data] = n - 1;
            }
            if (map.Count == 0) _hashOuter.Value = null;
        }
    }

    // Hard ceiling on nested Data depth. STJ's own MaxDepth=64 caps a single
    // ParseValue call, but LiftDataIfShaped restarts STJ via
    // Deserialize<@this>(string, options) on each recursion — that resets
    // STJ's depth counter to zero, leaving only the C# call stack to bound
    // recursion (security v1 F1: pre-auth StackOverflow DoS at ~500 levels,
    // ~11 KB payload). The AsyncLocal counter below threads through every
    // Read invocation so the budget applies cumulatively across the
    // GetRawText round-trip. 64 mirrors STJ's default; throws JsonException
    // past it so the catch in plang.@this.DeserializeAsync turns it into a
    // typed PlangDeserializeError 400 rather than a crash.
    private const int MaxReadDepth = 64;
    private static readonly AsyncLocal<int> _readDepth = new();

    public override @this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null!;
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for app.data.@this wire shape");

        if (_readDepth.Value >= MaxReadDepth)
            throw new JsonException(
                $"app.data.@this wire shape nested past MaxReadDepth ({MaxReadDepth}) — payload rejected to prevent stack overflow.");
        _readDepth.Value++;
        try
        {
            return ReadBody(ref reader, options);
        }
        finally
        {
            _readDepth.Value--;
        }
    }

    private @this ReadBody(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        string name = "";
        object? value = null;
        type? typeRef = null;
        app.modules.signing.Signature? signature = null;
        Properties? properties = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                var data = new @this(name, value, typeRef);
                if (signature != null) data.Signature = signature;
                if (properties != null) data.Properties = properties;
                return data;
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
                    if (reader.TokenType == JsonTokenType.Null) typeRef = null;
                    else if (reader.TokenType == JsonTokenType.String)
                    {
                        var typeStr = reader.GetString();
                        typeRef = string.IsNullOrEmpty(typeStr) ? null : new type(typeStr);
                    }
                    else throw new JsonException("type field must be a JSON string");
                    break;
                case "value":
                    // Peek at the token to see whether the value slot is a
                    // nested Data (recognised by its {name, value, [signature]}
                    // shape) — STJ alone would surface a Dictionary because the
                    // destination type is object. Without rehydration the inner
                    // Data's Signature would be observable as a sub-dictionary
                    // but never reach signing.verify or App-level navigation.
                    if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        // Buffer the sub-object so we can decide.
                        using var doc = JsonDocument.ParseValue(ref reader);
                        value = LiftDataIfShaped(doc.RootElement, options);
                    }
                    else
                    {
                        value = JsonSerializer.Deserialize<object?>(ref reader, options);
                    }
                    break;
                case "signature":
                    signature = JsonSerializer.Deserialize<app.modules.signing.Signature>(ref reader, options);
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
                if (reader.TryGetDecimal(out var dec)) return dec;
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

    // A JSON object with both "name" and "value" keys is the canonical Data
    // wire shape (the WireJsonConverter always emits both on Write, including
    // the "value": null case). Domain types Identity / Signature etc. have
    // neither pair: Identity has "name" but no "value"; Signature has "type"
    // but no "name" or "value". Requiring both keys keeps rehydration
    // unambiguous across the value-graph without an explicit type marker.
    private static object? LiftDataIfShaped(System.Text.Json.JsonElement element, JsonSerializerOptions options)
    {
        bool hasName = false, hasValue = false;
        foreach (var prop in element.EnumerateObject())
        {
            if (!hasName && prop.Name.Equals("name", StringComparison.OrdinalIgnoreCase)) hasName = true;
            else if (!hasValue && prop.Name.Equals("value", StringComparison.OrdinalIgnoreCase)) hasValue = true;
            if (hasName && hasValue) break;
        }

        if (!(hasName && hasValue))
        {
            return JsonSerializer.Deserialize<object?>(element.GetRawText(), options);
        }

        return JsonSerializer.Deserialize<@this>(element.GetRawText(), options);
    }

    public override void Write(Utf8JsonWriter writer, @this data, JsonSerializerOptions options)
    {
        var isHashOuter = IsHashOuter(data);
        // Sign-if-missing — but only when the Data is wired into a real actor
        // scope (Context.Actor is non-null). A bare Context with no Actor is
        // the "internal in-memory serialise" case (test fixtures, .pr
        // authoring, raw IClass calls); signing has no identity to draw on
        // and skipping silently keeps those paths working. Production
        // discipline (Variables.Set / Channels.Register) always sets Actor
        // before any wire crossing.
        if (!isHashOuter && data.Signature == null && data.Context?.Actor != null)
        {
            data.EnsureSigned();
        }

        writer.WriteStartObject();
        writer.WriteString("name", data.Name);

        // type — emit as a plain JSON string (the data.@this.Type's wire form).
        // Skipped entirely when null to match the legacy [JsonIgnore(WhenWritingNull)]
        // discipline so the wire stays compact.
        var typeVal = data.Type?.Value;
        if (typeVal != null)
        {
            writer.WriteString("type", typeVal);
        }

        writer.WritePropertyName("value");
        JsonSerializer.Serialize(writer, data.Value, options);

        // properties — nested object, omitted when empty to keep the wire compact.
        // Sign-if-missing walks Value-graph Datas only, never Properties; the
        // outer Data's signature covers the canonicalized wire (including this
        // nested object), so tampering with any Property still invalidates the
        // outer signature, but no per-Property attestations are conjured.
        if (data.Properties.Count > 0)
        {
            writer.WritePropertyName("properties");
            writer.WriteStartObject();
            foreach (var kvp in data.Properties)
            {
                writer.WritePropertyName(kvp.Key);
                JsonSerializer.Serialize(writer, kvp.Value, options);
            }
            writer.WriteEndObject();
        }

        if (!isHashOuter && data.Signature != null)
        {
            writer.WritePropertyName("signature");
            JsonSerializer.Serialize(writer, data.Signature, options);
        }

        writer.WriteEndObject();
    }
}
