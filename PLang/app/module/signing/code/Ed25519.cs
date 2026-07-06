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
        var identityResult = await app.RunAction<identity.Get>(new identity.Get(action.Context), action.Context);
        if (!identityResult.Success) return identityResult;
        var identity = (Identity)(await identityResult.Value())!;

        // Hash the inner data — the digest binds the value into the signed bytes.
        var hashResult = await app.RunAction<Hash>(new Hash(action.Context) { Data = action.Data, Algorithm = new data.@this<global::app.type.text.@this>("", "keccak256", context: action.Context), StoreView = action.StoreView }, action.Context);
        if (!hashResult.Success) return hashResult;
        if (await hashResult.Value() is not global::app.module.crypto.type.hash.@this hash)
            return action.Context.Error(new ActionError("Hashing produced no digest", "DataHashMismatch", 500));

        var now = await (await action.Context.Variable.Get("NowUtc")).Clr<DateTimeOffset>(default);
        var nonce = (await (await action.Context.Variable.Get("GUID")).Clr<Guid>(default)).ToString();
        DateTimeOffset? expires = (action.Expires == null ? null : await action.Expires.Value()) is { } expiry
            ? now.Add(expiry.Value) : null;
        var contracts = action.Contracts == null ? null : await action.Contracts.Value();

        // Build the unsigned signature wrapping the data, compute its signing bytes,
        // sign with the identity's private key, stamp the signature in.
        var unsigned = new global::app.type.signature.@this(
            value: action.Data!,
            algorithm: new global::app.type.text.@this(Name),
            nonce: new global::app.type.text.@this(nonce),
            created: new global::app.type.datetime.@this(now),
            identity: new global::app.type.text.@this(identity.PublicKey),
            hash: hash,
            signature: new global::app.type.binary.@this(Array.Empty<byte>()),
            expires: expires is { } e ? new global::app.type.datetime.@this(e) : null,
            contracts: contracts);

        try
        {
            var signed = unsigned.Signed(Sign(unsigned, new global::app.type.text.@this(identity.PrivateKey)));
            return action.Context.Ok(signed);
        }
        catch (Exception ex)
        {
            return action.Context.Error(ActionError.FromException(ex, "SigningError", 500));
        }
    }

    public virtual async Task<data.@this<global::app.type.@bool.@this>> VerifyAsync(verify action)
    {
        if (action.Data?.Peek() is not global::app.type.signature.@this signature)
            return action.Context.Error<global::app.type.@bool.@this>(new ActionError("Data has no signature", "NoSignature", 400));

        var app = action.Context.App;
        // NowUtc may be unset when verify runs at the deserialize boundary (the
        // read context isn't mid-step) — fall back to the wall clock rather than
        // NRE on an unbox of a missing runtime variable.
        var now = await (await action.Context.Variable.Get("NowUtc")).Clr<DateTimeOffset>(DateTimeOffset.UtcNow);
        // T? convention — plang-null pass converts this (value-door-plang-null branch)
        long effectiveTimeout = (await action.TimeoutMs.Value())!.ToInt64();
        var skipFreshness = (action.SkipFreshnessCheck == null ? null : (await action.SkipFreshnessCheck.Value())?.Value) ?? false;

        // 1. Wire-freshness check (Created too old). Anti-replay primitive for
        // transient signed messages — skipped for long-lived artifacts (grants)
        // whose intrinsic lifetime is governed by step 2's Expires.
        if (!skipFreshness)
        {
            var age = now - signature.Created.Value;
            if (age.TotalMilliseconds > effectiveTimeout)
                return action.Context.Error<global::app.type.@bool.@this>(new ActionError($"Signature timed out (age: {age.TotalMilliseconds:F0}ms, timeout: {effectiveTimeout}ms)", "TimedOut", 400));
        }

        // 2. Expiry check (signature's intrinsic lifetime — null = permanent).
        if (signature.Expires is { } exp && now > exp.Value)
            return action.Context.Error<global::app.type.@bool.@this>(new ActionError("Signature has expired", "Expired", 400));

        // 3. Nonce replay check — paired with step 1 (wire-freshness). For
        // stored artifacts the same nonce naturally re-presents on every read,
        // which isn't replay; skip alongside step 1.
        if (!skipFreshness)
        {
            var nonceCacheKey = $"nonce:{signature.Nonce}";
            var cacheSettings = new CacheSettings { DurationMs = effectiveTimeout };
            var nonceAdded = await app.Cache.TryAddAsync(nonceCacheKey, action.Context.Ok(true), cacheSettings);
            if (!nonceAdded)
                return action.Context.Error<global::app.type.@bool.@this>(new ActionError("Nonce has already been used", "NonceReplay", 400));
        }

        // 4. Contract matching — Contracts may be an unset/absent slot (the
        // boundary-verify path never sets it), so guard the resolved value too.
        var contractsList = action.Contracts == null ? null : await action.Contracts.Value();
        var expectedContracts = contractsList == null ? null
            : System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(
                contractsList.Items, d => d.Peek().ToString() ?? ""));
        if (!ContractsMatch(System.Linq.Enumerable.ToList(signature.ContractStrings()), expectedContracts))
            return action.Context.Error<global::app.type.@bool.@this>(new ActionError("Contract mismatch", "ContractMismatch", 400));

        // 5. Data hash verification — rehash the inner value, compare to the
        // signed digest (which carries its own algorithm).
        var storedHash = signature.Hash;
        if (storedHash.Bytes.Length == 0)
            return action.Context.Error<global::app.type.@bool.@this>(new ActionError("Missing data hash", "DataHashMismatch", 400));

        // Re-hash in the same view the data was stored in. skipFreshness == (View == Store)
        // (set by the reader / the Store-view caller), so it doubles as the hash view: a stored
        // value is a property-bag carrying every [Store] field; hashing it in Out view (a subset)
        // would diverge from the sign-time Store hash.
        var rehash = await app.RunAction<Hash>(
            new Hash(action.Context) { Data = signature.Value, Algorithm = new data.@this<global::app.type.text.@this>("", storedHash.Algorithm, context: action.Context),
                       StoreView = new data.@this<global::app.type.@bool.@this>("", skipFreshness, context: action.Context) }, action.Context);
        if (!rehash.Success) return global::app.data.@this<global::app.type.@bool.@this>.From(rehash);
        if (await rehash.Value() is not global::app.module.crypto.type.hash.@this rehashValue || !rehashValue.DigestEquals(storedHash))
            return action.Context.Error<global::app.type.@bool.@this>(new ActionError("Data hash does not match signed hash", "DataHashMismatch", 400));

        // 6. Signature verification — over the signature's canonical signing bytes.
        if (signature.Signature.Value.Length == 0)
            return action.Context.Error<global::app.type.@bool.@this>(new ActionError("Missing signature", "SignatureInvalid", 400));

        try
        {
            if (!Verify(signature).Value)
                return action.Context.Error<global::app.type.@bool.@this>(new ActionError("Signature verification failed", "SignatureInvalid", 400));
            return action.Context.Ok<global::app.type.@bool.@this>(true);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException
            or System.Security.Cryptography.CryptographicException or InvalidOperationException)
        {
            return action.Context.Error<global::app.type.@bool.@this>(ActionError.FromException(ex, "SignatureInvalid", 400));
        }
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

    // Low-level crypto primitives: no context (a shared provider serves every actor), so they
    // return the VALUE and THROW on failure. The context-ful [Code] boundary (SignAsync /
    // VerifyAsync, which hold action.Context) borns the data.error. Same shape as number ops.
    // They speak plang types; CLR appears only at the NSec call (the 3rd-party perimeter), where
    // the signature is decomposed into its signing bytes / identity / signature bytes.
    public global::app.type.binary.@this Sign(global::app.type.signature.@this unsigned, global::app.type.text.@this privateKey)
    {
        var algorithm = SignatureAlgorithm.Ed25519;
        var privateKeyBytes = Convert.FromBase64String(privateKey.ToString());
        using var key = Key.Import(algorithm, privateKeyBytes, KeyBlobFormat.RawPrivateKey,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
        return new global::app.type.binary.@this(algorithm.Sign(key, unsigned.ToSigningBytes()));
    }

    public global::app.type.@bool.@this Verify(global::app.type.signature.@this signature)
    {
        var algorithm = SignatureAlgorithm.Ed25519;
        var publicKeyBytes = Convert.FromBase64String(signature.Identity.ToString());
        var nsecPublicKey = NSec.Cryptography.PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);
        return new global::app.type.@bool.@this(algorithm.Verify(nsecPublicKey, signature.ToSigningBytes(), signature.Signature.Value));
    }
}
