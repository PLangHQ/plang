using System.Security.Cryptography;
using NSec.Cryptography;
using app.error;
using app.variable;
using app.config;
using app.module.code;
using app.module.crypto;
using app.module.identity;

namespace app.module.signing.code;

/// <summary>
/// Ed25519 signing provider. Owns the full signing/verification pipeline.
/// Low-level crypto via NSec. High-level pipeline builds Signature objects.
/// </summary>
public class Ed25519 : ISigning
{
    public string Name => "ed25519";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    // --- High-level pipeline ---

    public virtual async Task<data.@this> SignAsync(sign action)
    {
        var app = action.Context.App;

        // Get identity
        var identityResult = await app.RunAction<identity.Get>(new identity.Get(), action.Context);
        if (!identityResult.Success) return identityResult;
        var identity = (Identity)(await identityResult.Value())!;

        // Hash the data
        var hash = await app.RunAction<Hash>(new Hash { Data = action.Data, Algorithm = new data.@this<global::app.type.text.@this>("", "keccak256") }, action.Context);
        if (!hash.Success) return hash;

        var now = (DateTimeOffset)(await action.Context.Variable.GetValue("NowUtc"))!;
        var nonce = (await action.Context.Variable.GetValue("GUID"))!.ToString()!;

        var signedData = new Signature
        {
            Type = "signature",
            Algorithm = Name,
            Nonce = nonce,
            Created = now,
            Expires = (action.Expires == null ? null : await action.Expires.Value()) is { } expiry ? now.Add(expiry) : null,
            Contracts = action.Contracts == null ? null
                : global::app.type.item.@this.Lower<List<string>>(await action.Contracts.Value()),
            Headers = action.Headers == null ? null
                : global::app.type.item.@this.Lower<Dictionary<string, object>>(await action.Headers.Value()),
            Hash = hash
        };

        signedData.Identity = identity.PublicKey;
        var signingBytes = signedData.ToSigningBytes();
        var signResult = Sign(signingBytes, identity.PrivateKey);
        if (!signResult.Success) return signResult;
        signedData.Value = Convert.ToBase64String((byte[])(await signResult.Value())!);

        action.Data!.Signature = signedData;
        return action.Data;
    }

    public virtual async Task<data.@this<global::app.type.@bool.@this>> VerifyAsync(verify action)
    {
        if (action.Data?.Signature == null)
            return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Data has no signature", "NoSignature", 400));

        var signedData = action.Data.Signature;
        var app = action.Context.App;
        var now = (DateTimeOffset)await action.Context.Variable.GetValue("NowUtc")!;
        var signingSettings = app.Config.For<Config>(action.Context);
        long effectiveTimeout = action.TimeoutMs == null
            ? signingSettings.Resolve<long>("TimeoutMs", 300_000)
            : (await action.TimeoutMs.Value())?.ToInt64() ?? signingSettings.Resolve<long>("TimeoutMs", 300_000);
        var skipFreshness = (action.SkipFreshnessCheck == null ? null : (await action.SkipFreshnessCheck.Value())?.Value) ?? false;

        // 1. Type check
        if (signedData.Type != "signature")
            return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError($"Invalid signed data type: '{signedData.Type}'", "InvalidType", 400));

        // 2. Wire-freshness check (Created too old). Anti-replay primitive for
        // transient signed messages — skipped for long-lived artifacts (grants)
        // whose intrinsic lifetime is governed by step 3's Expires.
        if (!skipFreshness)
        {
            var age = now - signedData.Created;
            if (age.TotalMilliseconds > effectiveTimeout)
                return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError($"Signature timed out (age: {age.TotalMilliseconds:F0}ms, timeout: {effectiveTimeout}ms)", "TimedOut", 400));
        }

        // 3. Expiry check (signature's intrinsic lifetime — null = permanent).
        if (signedData.Expires.HasValue && now > signedData.Expires.Value)
            return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Signature has expired", "Expired", 400));

        // 4. Nonce replay check — paired with step 2 (wire-freshness). For
        // stored artifacts the same nonce naturally re-presents on every read,
        // which isn't replay; skip alongside step 2.
        if (!skipFreshness)
        {
            var nonceCacheKey = $"nonce:{signedData.Nonce}";
            var cacheSettings = new CacheSettings { DurationMs = effectiveTimeout };
            var nonceAdded = await app.Cache.TryAddAsync(nonceCacheKey, global::app.data.@this.Ok(true), cacheSettings);
            if (!nonceAdded)
                return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Nonce has already been used", "NonceReplay", 400));
        }

        // 5. Contract matching
        var expectedContracts = action.Contracts == null ? null
            : global::app.type.item.@this.Lower<List<string>>(await action.Contracts.Value());
        if (!ContractsMatch(signedData.Contracts, expectedContracts))
            return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Contract mismatch", "ContractMismatch", 400));

        // 6. Header matching — optional param: an absent slot binds non-null Uninitialized
        // (null model), so gate on the resolved value. The C# null check stays
        // for direct C# composition, where `init` can set the property to null.
        if (action.Headers != null
            && global::app.type.item.@this.Lower<Dictionary<string, object>>(await action.Headers.Value()) is { } expectedHeaders)
        {
            if (signedData.Headers == null)
                return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Signed data has no headers but verification expects headers", "HeaderMismatch", 400));

            foreach (var kvp in expectedHeaders)
            {
                if (!signedData.Headers.TryGetValue(kvp.Key, out var signedValue))
                    return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError($"Header mismatch for '{kvp.Key}'", "HeaderMismatch", 400));

                // Constant-time comparison to prevent timing side-channel attacks
                var expectedBytes = System.Text.Encoding.UTF8.GetBytes(kvp.Value?.ToString() ?? "");
                var actualBytes = System.Text.Encoding.UTF8.GetBytes(signedValue?.ToString() ?? "");
                if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
                    return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError($"Header mismatch for '{kvp.Key}'", "HeaderMismatch", 400));
            }
        }

        // 7. Data hash verification — the stored digest is a hash value that
        // carries its own algorithm.
        if ((signedData.Hash == null ? null : await signedData.Hash.Value()) is not global::app.module.crypto.type.hash.@this storedHash || storedHash.Bytes.Length == 0)
            return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Missing data hash", "DataHashMismatch", 400));

        if ((action.Data == null ? null : await action.Data.Value()) != null)
        {
            var rehash = await app.RunAction<Hash>(
                new Hash { Data = action.Data, Algorithm = new data.@this<global::app.type.text.@this>("", storedHash.Algorithm) }, action.Context);
            if (!rehash.Success) return global::app.data.@this<global::app.type.@bool.@this>.From(rehash);
            if (await rehash.Value() is not global::app.module.crypto.type.hash.@this rehashValue || !rehashValue.DigestEquals(storedHash))
                return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Data hash does not match signed hash", "DataHashMismatch", 400));
        }

        // 8. Signature verification
        if (string.IsNullOrEmpty(signedData.Value))
            return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Missing signature", "SignatureInvalid", 400));

        byte[] signatureBytes;
        try { signatureBytes = Convert.FromBase64String(signedData.Value); }
        catch (FormatException) { return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Invalid base64 signature", "SignatureInvalid", 400)); }

        var signingBytes = signedData.ToSigningBytes();
        var verifyResult = Verify(signingBytes, signatureBytes, signedData.Identity);
        if (!verifyResult.Success) return global::app.data.@this<global::app.type.@bool.@this>.From(verifyResult);

        return global::app.data.@this<global::app.type.@bool.@this>.Ok(true);
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

    public (KeyPair? keys, global::app.error.IError? error) GenerateKeyPair()
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

            return (new KeyPair(
                Convert.ToBase64String(publicKeyBytes),
                Convert.ToBase64String(privateKeyBytes)), null);
        }
        catch (Exception ex)
        {
            return (null, ActionError.FromException(ex, "KeyGenerationError", 500));
        }
    }

    public data.@this<global::app.type.binary.@this> Sign(byte[] data, string privateKeyBase64)
    {
        try
        {
            var algorithm = SignatureAlgorithm.Ed25519;
            var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);

            using var key = Key.Import(algorithm, privateKeyBytes, KeyBlobFormat.RawPrivateKey,
                new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

            var signature = algorithm.Sign(key, data);
            return global::app.data.@this<global::app.type.binary.@this>.Ok(signature);
        }
        catch (Exception ex)
        {
            return global::app.data.@this<global::app.type.binary.@this>.FromError(ActionError.FromException(ex, "SigningError", 500));
        }
    }

    public data.@this<global::app.type.@bool.@this> Verify(byte[] data, byte[] signature, string publicKeyBase64)
    {
        try
        {
            var algorithm = SignatureAlgorithm.Ed25519;
            var publicKeyBytes = Convert.FromBase64String(publicKeyBase64);

            var publicKey = NSec.Cryptography.PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);
            var isValid = algorithm.Verify(publicKey, data, signature);

            if (!isValid)
                return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Signature verification failed", "SignatureInvalid", 400));

            return global::app.data.@this<global::app.type.@bool.@this>.Ok(true);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException
            or System.Security.Cryptography.CryptographicException or InvalidOperationException)
        {
            return global::app.data.@this<global::app.type.@bool.@this>.FromError(ActionError.FromException(ex, "SignatureInvalid", 400));
        }
    }
}
