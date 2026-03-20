using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.crypto;
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

    [JsonPropertyOrder(9), JsonInclude]
    public HashedData HashedData { get; internal set; } = new();

    [JsonPropertyOrder(10), JsonInclude]
    public string? Signature { get; internal set; }

    [JsonIgnore]
    private Data? _verified;

    [JsonIgnore]
    internal Engine.@this? _engine;

    // --- Creation (behavior owned by SignedData) ---

    /// <summary>
    /// Creates a signed envelope from a sign action record. Navigates the action for
    /// data, contracts, headers, TTL, and provider name. Returns the final Data with
    /// Signature attached — ready to return from the handler.
    /// </summary>
    internal static async Task<Data> CreateAsync(sign action, Engine.@this engine)
    {
        var effectiveContracts = action.Contracts ?? new List<string> { "C0" };
        if (effectiveContracts.Count == 0)
            return Data.FromError(new ActionError("At least one contract is required", "ValidationError", 400));

        // Resolve signing provider
        ISigningProvider? signingProvider;
        if (!string.IsNullOrEmpty(action.Provider))
        {
            signingProvider = engine.Providers.Get<ISigningProvider>(action.Provider);
            if (signingProvider == null)
                return Data.FromError(new ActionError($"Signing provider '{action.Provider}' not found", "ProviderNotFound", 404));
        }
        else
        {
            signingProvider = engine.Providers.Get<ISigningProvider>() ?? new Ed25519Provider();
        }

        // Get identity
        IdentityVariable identity;
        try
        {
            identity = await IdentityVariable.GetOrCreateDefaultAsync(engine);
        }
        catch (Exception ex)
        {
            return Data.FromError(ActionError.FromException(ex, "IdentityError", 500));
        }

        // Hash the data
        var data = action.Data ?? new object();
        var (bytes, format) = Hash.SerializeData(data);
        var cryptoProvider = Hash.ResolveProvider(engine);
        var hashResult = cryptoProvider.Hash(bytes, "sha256");
        if (!hashResult.Success) return hashResult;

        var now = DateTimeOffset.UtcNow;

        var signedData = new SignedData
        {
            Type = "signature",
            Algorithm = signingProvider.Name,
            Nonce = Guid.NewGuid().ToString("N"),
            Created = now,
            Expires = action.ExpiresInMs.HasValue ? now.AddMilliseconds(action.ExpiresInMs.Value) : null,
            Identity = identity.PublicKey,
            Contracts = effectiveContracts,
            Headers = action.Headers,
            HashedData = new HashedData
            {
                Algorithm = "sha256",
                Format = format,
                Hash = Hash.FormatHash((byte[])hashResult.Value!)
            },
            _engine = engine
        };

        // Sign: serialize with null Signature, then populate
        var signingBytes = signedData.ToSigningBytes();

        byte[] signatureBytes;
        try
        {
            signatureBytes = signingProvider.Sign(signingBytes, identity.PrivateKey);
        }
        catch (Exception ex)
        {
            return Data.FromError(ActionError.FromException(ex, "SigningError", 500));
        }

        signedData.Signature = Convert.ToBase64String(signatureBytes);

        // Return the final composed result — handler relays directly
        var result = Data.Ok(action.Data);
        result.Signature = signedData;
        return result;
    }

    // --- Verification (behavior owned by SignedData) ---

    /// <summary>
    /// Verifies this signed envelope from a verify action record. Navigates the action
    /// for contracts, headers, timeoutMs, and original data (from action.Data.Value).
    /// </summary>
    public async Task<Data> VerifyAsync(verify action)
    {
        if (_engine == null)
            return Data.FromError(new ActionError("No engine attached — cannot verify", "VerifyError", 500));

        var result = await VerifyInternalAsync(action.Contracts, action.Headers, action.TimeoutMs, action.Data?.Value);
        _verified = result;
        return result;
    }

    /// <summary>
    /// Verifies with explicit parameters. Used by tests and ad-hoc verification
    /// when no action record is available.
    /// </summary>
    public async Task<Data> VerifyAsync(
        List<string>? requiredContracts, Dictionary<string, object>? expectedHeaders,
        long? timeoutMs, object? originalData = null)
    {
        if (_engine == null)
            return Data.FromError(new ActionError("No engine attached — cannot verify", "VerifyError", 500));

        var result = await VerifyInternalAsync(requiredContracts, expectedHeaders, timeoutMs, originalData);
        _verified = result;
        return result;
    }

    /// <summary>
    /// Lazy verification with default contracts ["C0"]. Async — no sync-over-async.
    /// Returns cached result on subsequent calls.
    /// </summary>
    public async Task<Data?> GetVerifiedAsync()
    {
        if (_verified != null) return _verified;
        if (_engine == null) return null;

        _verified = await VerifyInternalAsync(new List<string> { "C0" }, null, null, null);
        return _verified;
    }

    /// <summary>
    /// Returns the cached verification result without triggering verification.
    /// Null if not yet verified.
    /// </summary>
    [JsonIgnore]
    public Data? Verified => _verified;

    private async Task<Data> VerifyInternalAsync(
        List<string>? requiredContracts, Dictionary<string, object>? expectedHeaders,
        long? timeoutMs, object? originalData)
    {
        // 1. Type check
        if (Type != "signature")
            return Data.FromError(new ActionError($"Invalid signed data type: '{Type}'", "InvalidType", 400));

        // 2. Provider resolution from Algorithm field
        var provider = _engine!.Providers.Get<ISigningProvider>(Algorithm);
        if (provider == null && Algorithm == "ed25519")
            provider = new Ed25519Provider();
        if (provider == null)
            return Data.FromError(new ActionError($"Signing provider '{Algorithm}' not found", "ProviderNotFound", 404));

        // 3. Timeout check (Created too old)
        var effectiveTimeout = timeoutMs ?? 300_000; // 5 minutes default
        var age = DateTimeOffset.UtcNow - Created;
        if (age.TotalMilliseconds > effectiveTimeout)
            return Data.FromError(new ActionError($"Signature timed out (age: {age.TotalMilliseconds:F0}ms, timeout: {effectiveTimeout}ms)", "TimedOut", 400));

        // 4. Expiry check
        if (Expires.HasValue && DateTimeOffset.UtcNow > Expires.Value)
            return Data.FromError(new ActionError("Signature has expired", "Expired", 400));

        // 5. Nonce replay check
        var nonceCacheKey = $"nonce:{Nonce}";
        var cacheSeconds = (long)Math.Ceiling(effectiveTimeout / 1000.0);
        var cacheSettings = new CacheSettings { DurationSeconds = cacheSeconds };
        var nonceAdded = await _engine.Cache.TryAddAsync(nonceCacheKey, true, cacheSettings);
        if (!nonceAdded)
            return Data.FromError(new ActionError("Nonce has already been used", "NonceReplay", 400));

        // 6. Contract matching
        if (requiredContracts == null || requiredContracts.Count == 0)
            return Data.FromError(new ActionError("Required contracts must be specified", "ContractMismatch", 400));

        if (Contracts == null || Contracts.Count == 0)
            return Data.FromError(new ActionError("Signed data has no contracts", "ContractMismatch", 400));

        var requiredSet = new HashSet<string>(requiredContracts, StringComparer.OrdinalIgnoreCase);
        var signedSet = new HashSet<string>(Contracts, StringComparer.OrdinalIgnoreCase);
        if (!requiredSet.SetEquals(signedSet))
            return Data.FromError(new ActionError("Contract mismatch", "ContractMismatch", 400));

        // 7. Header matching (only checked if expected headers provided)
        if (expectedHeaders != null)
        {
            if (Headers == null)
                return Data.FromError(new ActionError("Signed data has no headers but verification expects headers", "HeaderMismatch", 400));

            foreach (var kvp in expectedHeaders)
            {
                if (!Headers.TryGetValue(kvp.Key, out var signedValue) ||
                    !string.Equals(signedValue?.ToString(), kvp.Value?.ToString(), StringComparison.Ordinal))
                    return Data.FromError(new ActionError($"Header mismatch for '{kvp.Key}'", "HeaderMismatch", 400));
            }
        }

        // 8. Data hash verification — re-hash original data if provided
        if (string.IsNullOrEmpty(HashedData?.Hash))
            return Data.FromError(new ActionError("Missing data hash", "DataHashMismatch", 400));

        if (originalData != null)
        {
            var (bytes, _) = Hash.SerializeData(originalData);
            var cryptoProvider = Hash.ResolveProvider(_engine);
            var rehashResult = cryptoProvider.Hash(bytes, HashedData!.Algorithm);
            if (!rehashResult.Success) return rehashResult;

            var expectedHash = Hash.FormatHash((byte[])rehashResult.Value!);
            if (!string.Equals(expectedHash, HashedData.Hash, StringComparison.Ordinal))
                return Data.FromError(new ActionError("Data hash does not match signed hash", "DataHashMismatch", 400));
        }

        // 9. Signature verification
        if (string.IsNullOrEmpty(Signature))
            return Data.FromError(new ActionError("Missing signature", "SignatureInvalid", 400));

        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(Signature);
        }
        catch (FormatException)
        {
            return Data.FromError(new ActionError("Invalid base64 signature", "SignatureInvalid", 400));
        }

        var signingBytes = ToSigningBytes();

        bool isValid;
        try
        {
            isValid = provider.Verify(signingBytes, signatureBytes, Identity);
        }
        catch (Exception ex)
        {
            return Data.FromError(ActionError.FromException(ex, "SignatureInvalid", 400));
        }

        if (!isValid)
            return Data.FromError(new ActionError("Signature verification failed", "SignatureInvalid", 400));

        return Data.Ok(true);
    }

    // --- Serialization ---

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
