using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.crypto;

namespace PLang.Runtime2.modules.signing;

/// <summary>
/// Verifies a signed data envelope. Checks in order:
/// NoSignature → ProviderNotFound → TimedOut → Expired → NonceReplay → ContractMismatch → HeaderMismatch → DataHashMismatch → SignatureInvalid
/// </summary>
[Action("verify", Cacheable = false)]
public partial class verify : IContext
{
    /// <summary>The signed data to verify.</summary>
    public partial Data? Data { get; init; }

    /// <summary>Required contracts for verification.</summary>
    public partial List<string>? Contracts { get; init; }

    /// <summary>Expected headers to match against signed headers.</summary>
    public partial Dictionary<string, object>? Headers { get; init; }

    /// <summary>Optional timeout override in milliseconds.</summary>
    public partial long? TimeoutMs { get; init; }

    public async Task<Data> Run()
    {
        var signedData = Data?.Signature;
        if (signedData == null)
            return Engine.Memory.Data.FromError(new ActionError("Data has no signature", "NoSignature", 400));

        var result = await VerifyCore(signedData, Contracts, Headers, TimeoutMs, Context.Engine);
        signedData.SetVerified(result);
        return result;
    }

    /// <summary>
    /// Core verification logic shared between explicit verify and lazy SignedData.Verified getter.
    /// </summary>
    internal static async Task<Data> VerifyCore(SignedData signedData, List<string>? requiredContracts,
        Dictionary<string, object>? expectedHeaders, long? timeoutMs,
        Engine.@this engine)
    {
        // 1. Type check
        if (signedData.Type != "signature")
            return Engine.Memory.Data.FromError(new ActionError($"Invalid signed data type: '{signedData.Type}'", "InvalidType", 400));

        // 2. Provider resolution from Algorithm field
        var provider = engine.Providers.Get<ISigningProvider>(signedData.Algorithm);
        if (provider == null && signedData.Algorithm == "ed25519")
            provider = new Ed25519Provider();
        if (provider == null)
            return Engine.Memory.Data.FromError(new ActionError($"Signing provider '{signedData.Algorithm}' not found", "ProviderNotFound", 404));

        // 3. Timeout check (Created too old)
        var effectiveTimeout = timeoutMs ?? 300_000; // 5 minutes default
        var age = DateTimeOffset.UtcNow - signedData.Created;
        if (age.TotalMilliseconds > effectiveTimeout)
            return Engine.Memory.Data.FromError(new ActionError($"Signature timed out (age: {age.TotalMilliseconds:F0}ms, timeout: {effectiveTimeout}ms)", "TimedOut", 400));

        // 4. Expiry check
        if (signedData.Expires.HasValue && DateTimeOffset.UtcNow > signedData.Expires.Value)
            return Engine.Memory.Data.FromError(new ActionError("Signature has expired", "Expired", 400));

        // 5. Nonce replay check
        var nonceCacheKey = $"nonce:{signedData.Nonce}";
        var cacheSeconds = (long)Math.Ceiling(effectiveTimeout / 1000.0);
        var cacheSettings = new CacheSettings { DurationSeconds = cacheSeconds };
        var nonceAdded = await engine.Cache.TryAddAsync(nonceCacheKey, true, cacheSettings);
        if (!nonceAdded)
            return Engine.Memory.Data.FromError(new ActionError("Nonce has already been used", "NonceReplay", 400));

        // 6. Contract matching
        if (requiredContracts == null || requiredContracts.Count == 0)
            return Engine.Memory.Data.FromError(new ActionError("Required contracts must be specified", "ContractMismatch", 400));

        if (signedData.Contracts == null || signedData.Contracts.Count == 0)
            return Engine.Memory.Data.FromError(new ActionError("Signed data has no contracts", "ContractMismatch", 400));

        var requiredSet = new HashSet<string>(requiredContracts, StringComparer.OrdinalIgnoreCase);
        var signedSet = new HashSet<string>(signedData.Contracts, StringComparer.OrdinalIgnoreCase);
        if (!requiredSet.SetEquals(signedSet))
            return Engine.Memory.Data.FromError(new ActionError("Contract mismatch", "ContractMismatch", 400));

        // 7. Header matching (only checked if expected headers provided)
        if (expectedHeaders != null)
        {
            if (signedData.Headers == null)
                return Engine.Memory.Data.FromError(new ActionError("Signed data has no headers but verification expects headers", "HeaderMismatch", 400));

            foreach (var kvp in expectedHeaders)
            {
                if (!signedData.Headers.TryGetValue(kvp.Key, out var signedValue) ||
                    !string.Equals(signedValue?.ToString(), kvp.Value?.ToString(), StringComparison.Ordinal))
                    return Engine.Memory.Data.FromError(new ActionError($"Header mismatch for '{kvp.Key}'", "HeaderMismatch", 400));
            }
        }

        // 8. Data hash verification
        if (string.IsNullOrEmpty(signedData.HashedData?.Hash))
            return Engine.Memory.Data.FromError(new ActionError("Missing data hash", "DataHashMismatch", 400));

        // 9. Signature verification
        if (string.IsNullOrEmpty(signedData.Signature))
            return Engine.Memory.Data.FromError(new ActionError("Missing signature", "SignatureInvalid", 400));

        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(signedData.Signature);
        }
        catch (FormatException)
        {
            return Engine.Memory.Data.FromError(new ActionError("Invalid base64 signature", "SignatureInvalid", 400));
        }

        var signingBytes = signedData.ToSigningBytes();

        bool isValid;
        try
        {
            isValid = provider.Verify(signingBytes, signatureBytes, signedData.Identity);
        }
        catch (Exception ex)
        {
            return Engine.Memory.Data.FromError(ActionError.FromException(ex, "SignatureInvalid", 400));
        }

        if (!isValid)
            return Engine.Memory.Data.FromError(new ActionError("Signature verification failed", "SignatureInvalid", 400));

        return Engine.Memory.Data.Ok(true);
    }
}
