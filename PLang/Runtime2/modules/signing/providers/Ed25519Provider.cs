using NSec.Cryptography;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Config;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.crypto;
using PLang.Runtime2.modules.identity;

namespace PLang.Runtime2.modules.signing.providers;

/// <summary>
/// Ed25519 signing provider. Owns the full signing/verification pipeline.
/// Low-level crypto via NSec. High-level pipeline builds SignedData envelopes.
/// </summary>
public class Ed25519Provider : ISigningProvider
{
    public string Name => "ed25519";
    public bool IsDefault { get; set; }

    // --- High-level pipeline ---

    public virtual async Task<Data> SignAsync(sign action)
    {
        var engine = action.Context.Engine;

        // Get identity
        var identityResult = await engine.RunAction<identity.Get>(new identity.Get(), action.Context);
        if (!identityResult.Success) return identityResult;
        var identity = (IdentityData)identityResult;

        // Hash the data
        var hash = await engine.RunAction<Hash>(new Hash { Data = action.Data, Algorithm = "keccak256" }, action.Context);
        if (!hash.Success) return hash;

        var now = (DateTimeOffset)action.Context.MemoryStack.GetValue("NowUtc")!;
        var nonce = action.Context.MemoryStack.GetValue("GUID")!.ToString()!;

        var signedData = new SignedData
        {
            Type = "signature",
            Algorithm = Name,
            Nonce = nonce,
            Created = now,
            Expires = action.ExpiresInMs.HasValue ? now.AddMilliseconds(action.ExpiresInMs.Value) : null,
            Contracts = action.Contracts,
            Headers = action.Headers,
            Hash = hash
        };

        signedData.Identity = identity.PublicKey;
        var signingBytes = signedData.ToSigningBytes();
        var signResult = Sign(signingBytes, identity.PrivateKey);
        if (!signResult.Success) return signResult;
        signedData.Signature = Convert.ToBase64String((byte[])signResult.Value!);

        action.Data!.Signature = signedData;
        return action.Data;
    }

    public virtual async Task<Data> VerifyAsync(verify action)
    {
        if (action.Data?.Signature == null)
            return Data.FromError(new ActionError("Data has no signature", "NoSignature", 400));

        var signedData = action.Data.Signature;
        var engine = action.Context.Engine;
        var now = (DateTimeOffset)action.Context.MemoryStack.GetValue("NowUtc")!;
        var signingSettings = engine.Config.For<Config>(action.Context);
        var effectiveTimeout = action.TimeoutMs ?? signingSettings.Resolve<long>("TimeoutMs", 300_000);

        // 1. Type check
        if (signedData.Type != "signature")
            return Data.FromError(new ActionError($"Invalid signed data type: '{signedData.Type}'", "InvalidType", 400));

        // 2. Timeout check (Created too old)
        var age = now - signedData.Created;
        if (age.TotalMilliseconds > effectiveTimeout)
            return Data.FromError(new ActionError($"Signature timed out (age: {age.TotalMilliseconds:F0}ms, timeout: {effectiveTimeout}ms)", "TimedOut", 400));

        // 3. Expiry check
        if (signedData.Expires.HasValue && now > signedData.Expires.Value)
            return Data.FromError(new ActionError("Signature has expired", "Expired", 400));

        // 4. Nonce replay check
        var nonceCacheKey = $"nonce:{signedData.Nonce}";
        var cacheSettings = new CacheSettings { DurationMs = effectiveTimeout };
        var nonceAdded = await engine.Cache.TryAddAsync(nonceCacheKey, Data.Ok(true), cacheSettings);
        if (!nonceAdded)
            return Data.FromError(new ActionError("Nonce has already been used", "NonceReplay", 400));

        // 5. Contract matching
        if (!ContractsMatch(signedData.Contracts, action.Contracts))
            return Data.FromError(new ActionError("Contract mismatch", "ContractMismatch", 400));

        // 6. Header matching
        if (action.Headers != null)
        {
            if (signedData.Headers == null)
                return Data.FromError(new ActionError("Signed data has no headers but verification expects headers", "HeaderMismatch", 400));

            foreach (var kvp in action.Headers)
            {
                if (!signedData.Headers.TryGetValue(kvp.Key, out var signedValue) ||
                    !string.Equals(signedValue?.ToString(), kvp.Value?.ToString(), StringComparison.Ordinal))
                    return Data.FromError(new ActionError($"Header mismatch for '{kvp.Key}'", "HeaderMismatch", 400));
            }
        }

        // 7. Data hash verification
        if (signedData.Hash?.Value is not byte[] storedHash || storedHash.Length == 0)
            return Data.FromError(new ActionError("Missing data hash", "DataHashMismatch", 400));

        if (action.Data?.Value != null)
        {
            var rehash = await engine.RunAction<Hash>(
                new Hash { Data = action.Data, Algorithm = signedData.Hash!.Type?.Value ?? "keccak256" }, action.Context);
            if (!rehash.Success) return rehash;
            if (rehash.Value is not byte[] rehashBytes || !rehashBytes.AsSpan().SequenceEqual(storedHash))
                return Data.FromError(new ActionError("Data hash does not match signed hash", "DataHashMismatch", 400));
        }

        // 8. Signature verification
        if (string.IsNullOrEmpty(signedData.Signature))
            return Data.FromError(new ActionError("Missing signature", "SignatureInvalid", 400));

        byte[] signatureBytes;
        try { signatureBytes = Convert.FromBase64String(signedData.Signature); }
        catch (FormatException) { return Data.FromError(new ActionError("Invalid base64 signature", "SignatureInvalid", 400)); }

        var signingBytes = signedData.ToSigningBytes();
        var verifyResult = Verify(signingBytes, signatureBytes, signedData.Identity);
        if (!verifyResult.Success) return verifyResult;

        return Data.Ok(true);
    }

    private static bool ContractsMatch(List<string>? signed, List<string>? required)
    {
        var hasRequired = required != null && required.Count > 0;
        var hasSigned = signed != null && signed.Count > 0;
        if (hasRequired != hasSigned) return false;
        if (!hasRequired) return true;
        return new HashSet<string>(required!, StringComparer.OrdinalIgnoreCase)
            .SetEquals(new HashSet<string>(signed!, StringComparer.OrdinalIgnoreCase));
    }

    // --- Low-level crypto ---

    public Data<KeyPair> GenerateKeyPair()
    {
        try
        {
            var algorithm = SignatureAlgorithm.Ed25519;

            using var key = Key.Create(algorithm, new KeyCreationParameters
            {
                ExportPolicy = KeyExportPolicies.AllowPlaintextExport
            });

            var publicKeyBytes = key.Export(KeyBlobFormat.RawPublicKey);
            var privateKeyBytes = key.Export(KeyBlobFormat.RawPrivateKey);

            return Data<KeyPair>.Ok(new KeyPair(
                Convert.ToBase64String(publicKeyBytes),
                Convert.ToBase64String(privateKeyBytes)
            ));
        }
        catch (Exception ex)
        {
            return Data<KeyPair>.FromError(ActionError.FromException(ex, "KeyGenerationError", 500));
        }
    }

    public Data Sign(byte[] data, string privateKeyBase64)
    {
        try
        {
            var algorithm = SignatureAlgorithm.Ed25519;
            var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);

            using var key = Key.Import(algorithm, privateKeyBytes, KeyBlobFormat.RawPrivateKey,
                new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

            var signature = algorithm.Sign(key, data);
            return Data.Ok(signature);
        }
        catch (Exception ex)
        {
            return Data.FromError(ActionError.FromException(ex, "SigningError", 500));
        }
    }

    public Data Verify(byte[] data, byte[] signature, string publicKeyBase64)
    {
        try
        {
            var algorithm = SignatureAlgorithm.Ed25519;
            var publicKeyBytes = Convert.FromBase64String(publicKeyBase64);

            var publicKey = NSec.Cryptography.PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);
            var isValid = algorithm.Verify(publicKey, data, signature);

            if (!isValid)
                return Data.FromError(new ActionError("Signature verification failed", "SignatureInvalid", 400));

            return Data.Ok(true);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException
            or System.Security.Cryptography.CryptographicException or InvalidOperationException)
        {
            return Data.FromError(ActionError.FromException(ex, "SignatureInvalid", 400));
        }
    }
}
