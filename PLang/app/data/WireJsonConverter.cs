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
    private static readonly AsyncLocal<HashSet<@this>?> _hashOuter = new();

    /// <summary>
    /// Marks <paramref name="data"/> as the "outer being hashed right now."
    /// While the returned scope is alive, the converter writes that one
    /// Data's body without invoking <see cref="@this.EnsureSigned"/> and
    /// without emitting its Signature field.
    /// </summary>
    public static IDisposable MarkOuterForHash(@this data)
    {
        var set = _hashOuter.Value ??= new HashSet<@this>(ReferenceEqualityComparer.Instance);
        set.Add(data);
        return new Scope(data);
    }

    private static bool IsHashOuter(@this data)
        => _hashOuter.Value?.Contains(data) == true;

    private sealed class Scope : IDisposable
    {
        private readonly @this _data;
        private bool _disposed;
        public Scope(@this data) { _data = data; }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var set = _hashOuter.Value;
            set?.Remove(_data);
            if (set != null && set.Count == 0) _hashOuter.Value = null;
        }
    }

    public override @this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null!;
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for app.data.@this wire shape");

        string name = "";
        object? value = null;
        type? typeRef = null;
        app.modules.signing.Signature? signature = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                var data = new @this(name, value, typeRef);
                if (signature != null) data.Signature = signature;
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
                    value = JsonSerializer.Deserialize<object?>(ref reader, options);
                    break;
                case "signature":
                    signature = JsonSerializer.Deserialize<app.modules.signing.Signature>(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        throw new JsonException("Unterminated app.data.@this wire shape");
    }

    public override void Write(Utf8JsonWriter writer, @this data, JsonSerializerOptions options)
    {
        var isHashOuter = IsHashOuter(data);
        // Sign-if-missing — but only when the Data is actually Context-wired.
        // A Data with no Context has no signing identity available; production
        // discipline routes through Variables.Set / Channels which wire Context
        // before any wire crossing, so the no-Context case in practice means
        // an internal in-memory serialise (e.g. .pr authoring) that doesn't
        // need an attestation. Skipping silently keeps that path working
        // without re-introducing the "outermost-only" rule the converter
        // replaces.
        if (!isHashOuter && data.RawSignature == null && data.Context != null)
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

        if (!isHashOuter && data.RawSignature != null)
        {
            writer.WritePropertyName("signature");
            JsonSerializer.Serialize(writer, data.RawSignature, options);
        }

        writer.WriteEndObject();
    }
}
