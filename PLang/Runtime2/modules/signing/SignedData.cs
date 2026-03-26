using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.crypto;
using PLang.Runtime2.modules.signing.providers;
using PLang.Runtime2.modules.identity;

namespace PLang.Runtime2.modules.signing;

/// <summary>
/// The signed data envelope. Owns creation (signing) and verification.
/// All fields are serialized in a deterministic order for signature verification.
/// </summary>
public class SignedData
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
    public Data Hash { get; internal set; } = Data.Ok("");

    /// <summary>
    /// Serializes Data as { "type": "algorithm", "value": "base64hash" } in the signing envelope.
    /// </summary>
    internal class HashDataConverter : JsonConverter<Data>
    {
        public override Data Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
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
            var typeObj = string.IsNullOrEmpty(type) ? null : Engine.Memory.Type.FromName(type);
            byte[] bytes;
            try { bytes = Convert.FromBase64String(value); } catch { bytes = Array.Empty<byte>(); }
            return Data.Ok(bytes, typeObj);
        }

        public override void Write(Utf8JsonWriter writer, Data value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("type", value.Type?.Value ?? "");
            var base64 = value.Value is byte[] bytes ? Convert.ToBase64String(bytes) : value.Value?.ToString() ?? "";
            writer.WriteString("value", base64);
            writer.WriteEndObject();
        }
    }

    [JsonPropertyOrder(10), JsonInclude]
    public string? Signature { get; internal set; }

    // --- Signing (behavior owned by SignedData) ---

    /// <summary>
    /// Signs this envelope using the given provider and identity.
    /// Navigates the identity for public key (→ Identity field) and private key (→ signing).
    /// </summary>
    public Data Sign(ISigningProvider provider, IdentityData identity)
    {
        Identity = identity.PublicKey;
        var signingBytes = ToSigningBytes();
        var signResult = provider.Sign(signingBytes, identity.PrivateKey);
        if (!signResult.Success) return signResult;

        Signature = Convert.ToBase64String((byte[])signResult.Value!);
        return Data.Ok(this);
    }

    /// <summary>
    /// Verifies the cryptographic signature on this envelope.
    /// Mirrors Sign() — SignedData owns both sides.
    /// </summary>
    public Data Verify(ISigningProvider provider)
    {
        if (string.IsNullOrEmpty(Signature))
            return Data.FromError(new ActionError("Missing signature", "SignatureInvalid", 400));

        byte[] signatureBytes;
        try { signatureBytes = Convert.FromBase64String(Signature); }
        catch (FormatException) { return Data.FromError(new ActionError("Invalid base64 signature", "SignatureInvalid", 400)); }

        var signingBytes = ToSigningBytes();
        return provider.Verify(signingBytes, signatureBytes, Identity);
    }

    // --- Contract matching ---

    /// <summary>
    /// Checks whether this envelope's contracts match the required contracts.
    /// Both null/empty is a match. Both present must be set-equal (case-insensitive).
    /// </summary>
    public bool ContractsMatch(List<string>? requiredContracts)
    {
        var hasRequired = requiredContracts != null && requiredContracts.Count > 0;
        var hasSigned = Contracts != null && Contracts.Count > 0;

        if (hasRequired != hasSigned) return false;
        if (!hasRequired) return true;

        var requiredSet = new HashSet<string>(requiredContracts!, StringComparer.OrdinalIgnoreCase);
        var signedSet = new HashSet<string>(Contracts!, StringComparer.OrdinalIgnoreCase);
        return requiredSet.SetEquals(signedSet);
    }

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
                    if (typeInfo.Type != typeof(SignedData)) return;
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
    /// Serializes this SignedData to deterministic JSON bytes for signing/verification.
    /// Thread-safe: Signature is excluded via JsonSerializerOptions, not by mutation.
    /// </summary>
    internal byte[] ToSigningBytes()
    {
        return JsonSerializer.SerializeToUtf8Bytes(this, SigningOptions);
    }
}
