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

    // --- Signing (behavior owned by SignedData) ---

    /// <summary>
    /// Signs this envelope using the given provider and identity.
    /// Navigates the identity for public key (→ Identity field) and private key (→ signing).
    /// </summary>
    public Data Sign(ISigningProvider provider, IdentityVariable identity)
    {
        Identity = identity.PublicKey;
        var signingBytes = ToSigningBytes();
        var signResult = provider.Sign(signingBytes, identity.PrivateKey);
        if (!signResult.Success) return signResult;

        Signature = Convert.ToBase64String((byte[])signResult.Value!);
        return Data.Ok(this);
    }

    // --- Creation ---

    /// <summary>
    /// Creates a signed envelope from a sign action record. Navigates the action for
    /// data, contracts, headers, TTL, and provider name. Returns the final Data with
    /// Signature attached — ready to return from the handler.
    /// </summary>
    internal static async Task<Data> CreateAsync(sign action)
    {
        var engine = action.Context.Engine;

        // Resolve signing provider: action param → settings → registry
        var signingSettings = engine.Settings.For<SigningConfig>(action.Context);
        var providerName = action.Provider ?? signingSettings.Resolve("Provider", "ed25519");

        var providerResult = engine.Providers.Get<ISigningProvider>(providerName);
        if (!providerResult.Success) return providerResult;

        // Get identity
        var identity = await engine.RunAction<identity.Get, IdentityVariable>(new identity.Get(), action.Context);
        if (!identity.Success) return identity;

        // Hash the data
        var hash = await engine.RunAction<Hash, HashedData>(new Hash { Data = action.Data ?? new object(), Algorithm = "keccak256" }, action.Context);
        if (!hash.Success) return hash;

        var now = (DateTimeOffset)action.Context.MemoryStack.GetValue("NowUtc")!;
        var nonce = action.Context.MemoryStack.GetValue("GUID")!.ToString()!;

        var signedData = new SignedData
        {
            Type = "signature",
            Algorithm = providerResult.Value!.Name,
            Nonce = nonce,
            Created = now,
            Expires = action.ExpiresInMs.HasValue ? now.AddMilliseconds(action.ExpiresInMs.Value) : null,
            Contracts = action.Contracts,
            Headers = action.Headers,
            HashedData = hash.Value!
        };

        var signResult = signedData.Sign(providerResult.Value, identity.Value!);
        if (!signResult.Success) return signResult;

        var result = Data.Ok(action.Data);
        result.Signature = signedData;
        return result;
    }

    // --- Verification (behavior owned by SignedData) ---

    /// <summary>
    /// Verifies this signed envelope from a verify action record. Navigates the action
    /// for contracts, headers, timeoutMs, and original data.
    /// </summary>
    public async Task<Data> VerifyAsync(verify action)
    {
        var engine = action.Context.Engine;
        var now = (DateTimeOffset)action.Context.MemoryStack.GetValue("NowUtc")!;
        var signingSettings = engine.Settings.For<SigningConfig>(action.Context);
        var effectiveTimeout = action.TimeoutMs ?? signingSettings.Resolve<long>("TimeoutMs", 300_000);

        // 1. Type check
        if (Type != "signature")
            return Data.FromError(new ActionError($"Invalid signed data type: '{Type}'", "InvalidType", 400));

        // 2. Provider resolution from Algorithm field
        var providerResult = engine.Providers.Get<ISigningProvider>(Algorithm);
        if (!providerResult.Success) return providerResult;

        // 3. Timeout check (Created too old)
        var age = now - Created;
        if (age.TotalMilliseconds > effectiveTimeout)
            return Data.FromError(new ActionError($"Signature timed out (age: {age.TotalMilliseconds:F0}ms, timeout: {effectiveTimeout}ms)", "TimedOut", 400));

        // 4. Expiry check
        if (Expires.HasValue && now > Expires.Value)
            return Data.FromError(new ActionError("Signature has expired", "Expired", 400));

        // 5. Nonce replay check
        var nonceCacheKey = $"nonce:{Nonce}";
        var cacheSettings = new CacheSettings { DurationMs = effectiveTimeout };
        var nonceAdded = await engine.Cache.TryAddAsync(nonceCacheKey, true, cacheSettings);
        if (!nonceAdded)
            return Data.FromError(new ActionError("Nonce has already been used", "NonceReplay", 400));

        // 6. Contract matching
        if (!ContractsMatch(action.Contracts))
            return Data.FromError(new ActionError("Contract mismatch", "ContractMismatch", 400));

        // 7. Header matching (only checked if expected headers provided)
        if (action.Headers != null)
        {
            if (Headers == null)
                return Data.FromError(new ActionError("Signed data has no headers but verification expects headers", "HeaderMismatch", 400));

            foreach (var kvp in action.Headers)
            {
                if (!Headers.TryGetValue(kvp.Key, out var signedValue) ||
                    !string.Equals(signedValue?.ToString(), kvp.Value?.ToString(), StringComparison.Ordinal))
                    return Data.FromError(new ActionError($"Header mismatch for '{kvp.Key}'", "HeaderMismatch", 400));
            }
        }

        // 8. Data hash verification — re-hash original data if provided
        if (string.IsNullOrEmpty(HashedData?.Hash))
            return Data.FromError(new ActionError("Missing data hash", "DataHashMismatch", 400));

        if (action.Data?.Value != null)
        {
            var rehash = await engine.RunAction<Hash, HashedData>(
                new Hash { Data = action.Data.Value, Algorithm = HashedData!.Algorithm }, action.Context);
            if (!rehash.Success) return rehash;
            if (!string.Equals(rehash.Value!.Hash, HashedData.Hash, StringComparison.Ordinal))
                return Data.FromError(new ActionError("Data hash does not match signed hash", "DataHashMismatch", 400));
        }

        // 9. Signature verification
        var verifyResult = Verify(providerResult.Value!);
        if (!verifyResult.Success) return verifyResult;

        return Data.Ok(true);
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
