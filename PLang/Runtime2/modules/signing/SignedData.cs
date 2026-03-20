using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.crypto;

namespace PLang.Runtime2.modules.signing;

/// <summary>
/// The signed data envelope. All fields are serialized in a deterministic order for signature verification.
/// The null-signature pattern: Signature is set to null during serialization of the signing payload,
/// then populated after signing.
/// </summary>
public class SignedData
{
    [JsonPropertyOrder(1)]
    public string Type { get; set; } = "signature";

    [JsonPropertyOrder(2)]
    public string Algorithm { get; set; } = "ed25519";

    [JsonPropertyOrder(3)]
    public string Nonce { get; set; } = "";

    [JsonPropertyOrder(4)]
    public DateTimeOffset Created { get; set; }

    [JsonPropertyOrder(5)]
    public DateTimeOffset? Expires { get; set; }

    [JsonPropertyOrder(6)]
    public string Identity { get; set; } = "";

    [JsonPropertyOrder(7)]
    public List<string>? Contracts { get; set; }

    [JsonPropertyOrder(8)]
    public Dictionary<string, object>? Headers { get; set; }

    [JsonPropertyOrder(9)]
    public HashedData HashedData { get; set; } = new();

    [JsonPropertyOrder(10)]
    public string? Signature { get; set; }

    /// <summary>
    /// Verification result. [JsonIgnore] to prevent leaking into the signed payload.
    /// Lazy verification: accessing this property triggers verification if _engine is set.
    /// </summary>
    [JsonIgnore]
    public Data? Verified
    {
        get
        {
            if (_verified != null) return _verified;
            if (_engine == null) return null;

            // Lazy verification: uses default contracts ["C0"] and no headers
            _verified = verify.VerifyCore(this, new List<string> { "C0" }, null, null, _engine).GetAwaiter().GetResult();
            return _verified;
        }
    }

    private Data? _verified;

    [JsonIgnore]
    internal Engine.@this? _engine;

    /// <summary>
    /// Sets the verification result explicitly (from the verify action).
    /// </summary>
    internal void SetVerified(Data result)
    {
        _verified = result;
    }

    /// <summary>
    /// Attaches the engine reference for lazy verification.
    /// </summary>
    internal void AttachEngine(Engine.@this engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Deterministic serialization options for signing.
    /// </summary>
    internal static readonly JsonSerializerOptions SigningOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>
    /// Serializes this SignedData to deterministic JSON bytes for signing/verification.
    /// </summary>
    internal byte[] ToSigningBytes()
    {
        var savedSig = Signature;
        Signature = null;
        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(this, SigningOptions);
        }
        finally
        {
            Signature = savedSig;
        }
    }
}
