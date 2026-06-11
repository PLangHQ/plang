using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using app.variable;

namespace app.module.signing;

/// <summary>
/// Cryptographic signature attached to a Data. Owns creation (signing) and verification.
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
    /// Serializes the data hash as <c>{ "type": "algorithm", "value": "base64digest" }</c>
    /// in the signing object. The <c>type</c> slot carries the algorithm (the
    /// hash's kind) — the deterministic signing bytes need it to recompute, and
    /// it round-trips a <see cref="app.module.crypto.type.hash.@this"/> value so
    /// the verify path reads the digest off the value, not loose bytes.
    /// </summary>
    internal class HashDataConverter : JsonConverter<data.@this>
    {
        public override data.@this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        {
            string algorithm = "", value = "";
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName) continue;
                var prop = reader.GetString();
                reader.Read();
                if (prop == "type") algorithm = reader.GetString() ?? "";
                else if (prop == "value") value = reader.GetString() ?? "";
            }
            if (string.IsNullOrEmpty(algorithm)) algorithm = "keccak256";
            byte[] bytes;
            try { bytes = Convert.FromBase64String(value); } catch (FormatException) { bytes = Array.Empty<byte>(); }
            var hashValue = new app.module.crypto.type.hash.@this(bytes, algorithm);
            return app.data.@this.Ok(hashValue, app.type.@this.Create("hash", kind: algorithm));
        }

        public override void Write(Utf8JsonWriter writer, data.@this value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            // The hash carries its own algorithm; fall back to the stamped kind
            // (or the keccak256 signing default) for a legacy bare-bytes hash.
            var algorithm = value.Peek() is app.module.crypto.type.hash.@this hash
                ? hash.Algorithm
                : value.Type?.Kind ?? "keccak256";
            writer.WriteString("type", algorithm);
            var base64 = value.Peek() switch
            {
                app.module.crypto.type.hash.@this h => h.ToBase64(),
                global::app.type.binary.@this b => Convert.ToBase64String(b.Value),
                _ => value.Peek()?.ToString() ?? ""
            };
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
