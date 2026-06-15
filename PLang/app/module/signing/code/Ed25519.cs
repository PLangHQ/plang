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

        // Hash the inner data — the digest binds the value into the signed bytes.
        var hashResult = await app.RunAction<Hash>(new Hash { Data = action.Data, Algorithm = new data.@this<global::app.type.text.@this>("", "keccak256") }, action.Context);
        if (!hashResult.Success) return hashResult;
        if (await hashResult.Value() is not global::app.module.crypto.type.hash.@this hash)
            return global::app.data.@this.FromError(new ActionError("Hashing produced no digest", "DataHashMismatch", 500));

        var now = (DateTimeOffset)(await action.Context.Variable.GetValue("NowUtc"))!;
        var nonce = (await action.Context.Variable.GetValue("GUID"))!.ToString()!;
        DateTimeOffset? expires = (action.Expires == null ? null : await action.Expires.Value()) is { } expiry
            ? now.Add(expiry.Value) : null;
        var contracts = action.Contracts == null ? null : await action.Contracts.Value();

        // Build the unsigned layer wrapping the data, compute its signing bytes,
        // sign with the identity's private key, stamp the signature in.
        var layer = new global::app.type.signature.@this(
            value: action.Data!,
            algorithm: new global::app.type.text.@this(Name),
            nonce: new global::app.type.text.@this(nonce),
            created: new global::app.type.datetime.@this(now),
            identity: new global::app.type.text.@this(identity.PublicKey),
            hash: hash,
            signature: new global::app.type.binary.@this(Array.Empty<byte>()),
            expires: expires is { } e ? new global::app.type.datetime.@this(e) : null,
            contracts: contracts);

        var signResult = Sign(layer.ToSigningBytes(), identity.PrivateKey);
        if (!signResult.Success) return signResult;
        var signed = layer.Signed((await signResult.Value())!);

        return global::app.data.@this.Ok(signed);
    }

    public virtual async Task<data.@this<global::app.type.@bool.@this>> VerifyAsync(verify action)
    {
        if (action.Data?.Peek() is not global::app.type.signature.@this layer)
            return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Data has no signature", "NoSignature", 400));

        var app = action.Context.App;
        // NowUtc may be unset when verify runs at the deserialize boundary (the
        // read context isn't mid-step) — fall back to the wall clock rather than
        // NRE on an unbox of a missing runtime variable.
        var now = await action.Context.Variable.GetValue("NowUtc") is DateTimeOffset nowUtc
            ? nowUtc : DateTimeOffset.UtcNow;
        var signingSettings = app.Config.For<Config>(action.Context);
        long effectiveTimeout = action.TimeoutMs == null
            ? signingSettings.Resolve<long>("TimeoutMs", 300_000)
            : (await action.TimeoutMs.Value())?.ToInt64() ?? signingSettings.Resolve<long>("TimeoutMs", 300_000);
        var skipFreshness = (action.SkipFreshnessCheck == null ? null : (await action.SkipFreshnessCheck.Value())?.Value) ?? false;

        // 1. Wire-freshness check (Created too old). Anti-replay primitive for
        // transient signed messages — skipped for long-lived artifacts (grants)
        // whose intrinsic lifetime is governed by step 2's Expires.
        if (!skipFreshness)
        {
            var age = now - layer.Created.Value;
            if (age.TotalMilliseconds > effectiveTimeout)
                return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError($"Signature timed out (age: {age.TotalMilliseconds:F0}ms, timeout: {effectiveTimeout}ms)", "TimedOut", 400));
        }

        // 2. Expiry check (signature's intrinsic lifetime — null = permanent).
        if (layer.Expires is { } exp && now > exp.Value)
            return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Signature has expired", "Expired", 400));

        // 3. Nonce replay check — paired with step 1 (wire-freshness). For
        // stored artifacts the same nonce naturally re-presents on every read,
        // which isn't replay; skip alongside step 1.
        if (!skipFreshness)
        {
            var nonceCacheKey = $"nonce:{layer.Nonce}";
            var cacheSettings = new CacheSettings { DurationMs = effectiveTimeout };
            var nonceAdded = await app.Cache.TryAddAsync(nonceCacheKey, global::app.data.@this.Ok(true), cacheSettings);
            if (!nonceAdded)
                return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Nonce has already been used", "NonceReplay", 400));
        }

        // 4. Contract matching — Contracts may be an unset/absent slot (the
        // boundary-verify path never sets it), so guard the resolved value too.
        var contractsList = action.Contracts == null ? null : await action.Contracts.Value();
        var expectedContracts = contractsList == null ? null
            : System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(
                contractsList.Items, d => d.Peek().ToString() ?? ""));
        if (!ContractsMatch(System.Linq.Enumerable.ToList(layer.ContractStrings()), expectedContracts))
            return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Contract mismatch", "ContractMismatch", 400));

        // 5. Data hash verification — rehash the inner value, compare to the
        // signed digest (which carries its own algorithm).
        var storedHash = layer.Hash;
        if (storedHash.Bytes.Length == 0)
            return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Missing data hash", "DataHashMismatch", 400));

        var rehash = await app.RunAction<Hash>(
            new Hash { Data = layer.Value, Algorithm = new data.@this<global::app.type.text.@this>("", storedHash.Algorithm) }, action.Context);
        if (!rehash.Success) return global::app.data.@this<global::app.type.@bool.@this>.From(rehash);
        if (await rehash.Value() is not global::app.module.crypto.type.hash.@this rehashValue || !rehashValue.DigestEquals(storedHash))
            return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Data hash does not match signed hash", "DataHashMismatch", 400));

        // 6. Signature verification — over the layer's canonical signing bytes.
        if (layer.Signature.Value.Length == 0)
            return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Missing signature", "SignatureInvalid", 400));

        var verifyResult = Verify(layer.ToSigningBytes(), layer.Signature.Value, layer.Identity.ToString());
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
