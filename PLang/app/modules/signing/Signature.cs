using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using app.variables;

namespace app.modules.signing;

/// <summary>
/// The signed data envelope. Owns creation (signing) and verification.
/// All fields are serialized in a deterministic order for signature verification.
/// </summary>
public class Signature
{
    [JsonPropertyOrder(1), JsonInclude]
    public string Type { get; internal set; } = "signature";

    [JsonPropertyOrder(2), JsonInclude]
    public string Algorithm { get; internal set; } = "ed25519";

    [JsonPropertyOrder(3), JsonInclude]
    public string Nonce { get; internal set; } = "";

    [JsonPropertyOrder(4), JsonInclude]
    public DateTimeOffset Created { get; internal set; }

    [JsonPropertyOrder(5), JsonInclude]
    public DateTimeOffset? Expires { get; internal set; }

    [JsonPropertyOrder(6), JsonInclude]
    public string Identity { get; internal set; } = "";

    [JsonPropertyOrder(7), JsonInclude]
    public List<string>? Contracts { get; internal set; }

    [JsonPropertyOrder(8), JsonInclude]
    public Dictionary<string, object>? Headers { get; internal set; }

    [JsonPropertyOrder(9), JsonPropertyName("hash"), JsonInclude]
    [JsonConverter(typeof(HashDataConverter))]
    public data.@this Hash { get; internal set; } = app.data.@this.Ok("");

    /// <summary>
    /// Serializes Data as { "type": "algorithm", "value": "base64hash" } in the signing envelope.
    /// </summary>
    internal class HashDataConverter : JsonConverter<data.@this>
    {
        public override data.@this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        {
            string type = "", value = "";
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName) continue;
                var prop = reader.GetString();
                reader.Read();
                if (prop == "type") type = reader.GetString() ?? "";
                else if (prop == "value") value = reader.GetString() ?? "";
            }
            var typeObj = string.IsNullOrEmpty(type) ? null : data.type.FromName(type);
            byte[] bytes;
            try { bytes = Convert.FromBase64String(value); } catch (FormatException) { bytes = Array.Empty<byte>(); }
            return app.data.@this.Ok(bytes, typeObj);
        }

        public override void Write(Utf8JsonWriter writer, data.@this value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("type", value.Type?.Value ?? "");
            var base64 = value.Value is byte[] bytes ? Convert.ToBase64String(bytes) : value.Value?.ToString() ?? "";
            writer.WriteString("value", base64);
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// Base64-encoded signature bytes. Renamed to <c>Value</c> after the
    /// <c>SignedData</c> → <c>Signature</c> type rename — the property name would
    /// otherwise collide with its enclosing class. Wire JSON key stays "signature"
    /// via <see cref="JsonPropertyNameAttribute"/> for backwards compatibility.
    /// </summary>
    [JsonPropertyOrder(10), JsonInclude, JsonPropertyName("signature")]
    public string? Value { get; internal set; }

    // --- Serialization ---

    /// <summary>
    /// Deterministic serialization options for signing.
    /// Excludes Signature property so ToSigningBytes is thread-safe (no mutation needed).
    /// </summary>
    internal static readonly JsonSerializerOptions SigningOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver
        {
            Modifiers =
            {
                static typeInfo =>
                {
                    if (typeInfo.Type != typeof(Signature)) return;
                    foreach (var prop in typeInfo.Properties)
                    {
                        if (prop.Name.Equals("signature", StringComparison.OrdinalIgnoreCase))
                            prop.ShouldSerialize = static (_, _) => false;
                    }
                }
            }
        }
    };

    /// <summary>
    /// Serializes this Signature to deterministic JSON bytes for signing/verification.
    /// Thread-safe: Signature is excluded via JsonSerializerOptions, not by mutation.
    /// </summary>
    internal byte[] ToSigningBytes()
    {
        return JsonSerializer.SerializeToUtf8Bytes(this, SigningOptions);
    }
}
