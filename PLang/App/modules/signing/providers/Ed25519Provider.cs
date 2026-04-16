using System.Security.Cryptography;
using NSec.Cryptography;
using App.Errors;
using App.Variables;
using App.Config;
using App.Providers;
using App.modules.crypto;
using App.modules.identity;

namespace App.modules.signing.providers;

/// <summary>
/// Ed25519 signing provider. Owns the full signing/verification pipeline.
/// Low-level crypto via NSec. High-level pipeline builds SignedData envelopes.
/// </summary>
public class Ed25519Provider : ISigningProvider
{
    public string Name => "ed25519";
    public bool IsDefault { get; set; }

    // --- High-level pipeline ---

    public virtual async Task<Data.@this> SignAsync(sign action)
    {
        var app = action.Context.App;

        // Get identity
        var identityResult = await app.RunAction<identity.Get>(new identity.Get(), action.Context);
        if (!identityResult.Success) return identityResult;
        var identity = (Identity)identityResult.Value!;

        // Hash the data
        var hash = await app.RunAction<Hash>(new Hash { Data = action.Data, Algorithm = new Data.@this<string>("", "keccak256") }, action.Context);
        if (!hash.Success) return hash;

        var now = (DateTimeOffset)action.Context.Variables.GetValue("NowUtc")!;
        var nonce = action.Context.Variables.GetValue("GUID")!.ToString()!;

        var signedData = new SignedData
        {
            Type = "signature",
            Algorithm = Name,
            Nonce = nonce,
            Created = now,
            Expires = action.ExpiresInMs?.Value is int expiryMs ? now.AddMilliseconds(expiryMs) : null,
            Contracts = action.Contracts?.Value,
            Headers = action.Headers?.Value,
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

    public virtual async Task<Data.@this> VerifyAsync(verify action)
    {
        if (action.Data?.Signature == null)
            return App.Data.@this.FromError(new ActionError("Data has no signature", "NoSignature", 400));

        var signedData = action.Data.Signature;
        var app = action.Context.App;
        var now = (DateTimeOffset)action.Context.Variables.GetValue("NowUtc")!;
        var signingSettings = app.Config.For<Config>(action.Context);
        var effectiveTimeout = action.TimeoutMs?.Value ?? signingSettings.Resolve<long>("TimeoutMs", 300_000);

        // 1. Type check
        if (signedData.Type != "signature")
            return App.Data.@this.FromError(new ActionError($"Invalid signed data type: '{signedData.Type}'", "InvalidType", 400));

        // 2. Timeout check (Created too old)
        var age = now - signedData.Created;
        if (age.TotalMilliseconds > effectiveTimeout)
            return App.Data.@this.FromError(new ActionError($"Signature timed out (age: {age.TotalMilliseconds:F0}ms, timeout: {effectiveTimeout}ms)", "TimedOut", 400));

        // 3. Expiry check
        if (signedData.Expires.HasValue && now > signedData.Expires.Value)
            return App.Data.@this.FromError(new ActionError("Signature has expired", "Expired", 400));

        // 4. Nonce replay check
        // Cache TTL matches effectiveTimeout. After restart, the timeout check (step 2)
        // rejects signatures older than effectiveTimeout, so nonce replay is bounded.
        var nonceCacheKey = $"nonce:{signedData.Nonce}";
        var cacheSettings = new CacheSettings { DurationMs = effectiveTimeout };
        var nonceAdded = await app.Cache.TryAddAsync(nonceCacheKey, App.Data.@this.Ok(true), cacheSettings);
        if (!nonceAdded)
            return App.Data.@this.FromError(new ActionError("Nonce has already been used", "NonceReplay", 400));

        // 5. Contract matching
        if (!ContractsMatch(signedData.Contracts, action.Contracts?.Value))
            return App.Data.@this.FromError(new ActionError("Contract mismatch", "ContractMismatch", 400));

        // 6. Header matching
        if (action.Headers?.Value != null)
        {
            if (signedData.Headers == null)
                return App.Data.@this.FromError(new ActionError("Signed data has no headers but verification expects headers", "HeaderMismatch", 400));

            foreach (var kvp in action.Headers.Value)
            {
                if (!signedData.Headers.TryGetValue(kvp.Key, out var signedValue))
                    return App.Data.@this.FromError(new ActionError($"Header mismatch for '{kvp.Key}'", "HeaderMismatch", 400));

                // Constant-time comparison to prevent timing side-channel attacks
                var expectedBytes = System.Text.Encoding.UTF8.GetBytes(kvp.Value?.ToString() ?? "");
                var actualBytes = System.Text.Encoding.UTF8.GetBytes(signedValue?.ToString() ?? "");
                if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
                    return App.Data.@this.FromError(new ActionError($"Header mismatch for '{kvp.Key}'", "HeaderMismatch", 400));
            }
        }

        // 7. Data hash verification
        if (signedData.Hash?.Value is not byte[] storedHash || storedHash.Length == 0)
            return App.Data.@this.FromError(new ActionError("Missing data hash", "DataHashMismatch", 400));

        if (action.Data?.Value != null)
        {
            var rehash = await app.RunAction<Hash>(
                new Hash { Data = action.Data, Algorithm = new Data.@this<string>("", signedData.Hash!.Type?.Value ?? "keccak256") }, action.Context);
            if (!rehash.Success) return rehash;
            if (rehash.Value is not byte[] rehashBytes || !rehashBytes.AsSpan().SequenceEqual(storedHash))
                return App.Data.@this.FromError(new ActionError("Data hash does not match signed hash", "DataHashMismatch", 400));
        }

        // 8. Signature verification
        if (string.IsNullOrEmpty(signedData.Signature))
            return App.Data.@this.FromError(new ActionError("Missing signature", "SignatureInvalid", 400));

        byte[] signatureBytes;
        try { signatureBytes = Convert.FromBase64String(signedData.Signature); }
        catch (FormatException) { return App.Data.@this.FromError(new ActionError("Invalid base64 signature", "SignatureInvalid", 400)); }

        var signingBytes = signedData.ToSigningBytes();
        var verifyResult = Verify(signingBytes, signatureBytes, signedData.Identity);
        if (!verifyResult.Success) return verifyResult;

        return App.Data.@this.Ok(true);
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

    public Data.@this<KeyPair> GenerateKeyPair()
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

            return Data.@this<KeyPair>.Ok(new KeyPair(
                Convert.ToBase64String(publicKeyBytes),
                Convert.ToBase64String(privateKeyBytes)
            ));
        }
        catch (Exception ex)
        {
            return Data.@this<KeyPair>.FromError(ActionError.FromException(ex, "KeyGenerationError", 500));
        }
    }

    public Data.@this Sign(byte[] data, string privateKeyBase64)
    {
        try
        {
            var algorithm = SignatureAlgorithm.Ed25519;
            var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);

            using var key = Key.Import(algorithm, privateKeyBytes, KeyBlobFormat.RawPrivateKey,
                new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

            var signature = algorithm.Sign(key, data);
            return App.Data.@this.Ok(signature);
        }
        catch (Exception ex)
        {
            return App.Data.@this.FromError(ActionError.FromException(ex, "SigningError", 500));
        }
    }

    public Data.@this Verify(byte[] data, byte[] signature, string publicKeyBase64)
    {
        try
        {
            var algorithm = SignatureAlgorithm.Ed25519;
            var publicKeyBytes = Convert.FromBase64String(publicKeyBase64);

            var publicKey = NSec.Cryptography.PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);
            var isValid = algorithm.Verify(publicKey, data, signature);

            if (!isValid)
                return App.Data.@this.FromError(new ActionError("Signature verification failed", "SignatureInvalid", 400));

            return App.Data.@this.Ok(true);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException
            or System.Security.Cryptography.CryptographicException or InvalidOperationException)
        {
            return App.Data.@this.FromError(ActionError.FromException(ex, "SignatureInvalid", 400));
        }
    }
}
