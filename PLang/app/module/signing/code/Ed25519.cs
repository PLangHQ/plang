using System.Security.Cryptography;
using NSec.Cryptography;
using app.error;
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
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    // --- High-level pipeline ---

    public virtual async Task<data.@this> SignAsync(sign action)
    {
        // Get identity
        var identityResult = await new global::app.goal.step.action.@this(new identity.Get(action.Context), action.Context).Start(action.Context);
        if (!identityResult.Success) return identityResult;
        var identity = (Identity)(await identityResult.Value())!;

        // Hash the inner data — the digest binds the value into the signed bytes.
        var hashResult = await new global::app.goal.step.action.@this(new Hash(action.Context) { Data = action.Data, Algorithm = new data.@this<global::app.type.item.text.@this>("", "keccak256", context: action.Context), StoreView = action.StoreView }, action.Context).Start(action.Context);
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
        var unsigned = new global::app.type.item.signature.@this(
            value: action.Data!,
            algorithm: new global::app.type.item.text.@this(Name),
            nonce: new global::app.type.item.text.@this(nonce),
            created: new global::app.type.item.datetime.@this(now),
            identity: new global::app.type.item.text.@this(identity.PublicKey),
            hash: hash,
            signature: new global::app.type.item.binary.@this(Array.Empty<byte>()),
            expires: expires is { } e ? new global::app.type.item.datetime.@this(e) : null,
            contracts: contracts);

        try
        {
            var signed = unsigned.Signed(Sign(unsigned, new global::app.type.item.text.@this(identity.PrivateKey)));
            return action.Context.Ok(signed);
        }
        catch (Exception ex)
        {
            return action.Context.Error(ActionError.FromException(ex, "SigningError", 500));
        }
    }

    public virtual async Task<data.@this<global::app.type.item.@bool.@this>> VerifyAsync(verify action)
    {
        if (action.Data?.Peek() is not global::app.type.item.signature.@this signature)
            return action.Context.Error<global::app.type.item.@bool.@this>(new ActionError("Data has no signature", "NoSignature", 400));

        var app = action.Context.App;

        // 1. Expiry — the signature answers it: what its signer signed, or, read live, its window after it was made.
        if (await signature.Expired(action.Context))
            return action.Context.Error<global::app.type.item.@bool.@this>(new ActionError(
                $"Signature has expired (at {signature.Expires!.Value:O})", "Expired", 400));

        // 2. Nonce replay — a signature read live presents its nonce once. One read from plang's own store
        // re-presents the same nonce on every read, which isn't replay. Only a look here: the nonce is
        // recorded once the hash and signature pass (step 6), so an unverified wire never uses one up.
        var nonceCacheKey = $"nonce:{signature.Nonce}";
        var live = signature.Origin.Value == global::app.type.item.signature.Origin.Live;
        if (live && await app.Cache.GetAsync(nonceCacheKey) != null)
            return action.Context.Error<global::app.type.item.@bool.@this>(new ActionError("Nonce has already been used", "NonceReplay", 400));

        // 3. Contract matching — Contracts may be an unset/absent slot (the
        // boundary-verify path never sets it), so guard the resolved value too.
        var contractsList = action.Contracts == null ? null : await action.Contracts.Value();
        var expectedContracts = contractsList == null ? null
            : System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(
                contractsList.Items(action.Context), d => d.Peek().ToString() ?? ""));
        if (!ContractsMatch(System.Linq.Enumerable.ToList(signature.ContractStrings()), expectedContracts))
            return action.Context.Error<global::app.type.item.@bool.@this>(new ActionError("Contract mismatch", "ContractMismatch", 400));

        // 4. Data hash verification — rehash the inner value, compare to the
        // signed digest (which carries its own algorithm).
        var storedHash = signature.Hash;
        if (storedHash.Bytes.Length == 0)
            return action.Context.Error<global::app.type.item.@bool.@this>(new ActionError("Missing data hash", "DataHashMismatch", 400));

        // Re-hash in the view the data was signed in: one read from plang's own store is a property-bag
        // carrying every [Store] field; hashing it in Out view (a subset) would diverge from the sign-time
        // Store hash.
        var rehash = await new global::app.goal.step.action.@this(
            new Hash(action.Context) { Data = signature.Value, Algorithm = new data.@this<global::app.type.item.text.@this>("", storedHash.Algorithm, context: action.Context),
                       StoreView = new data.@this<global::app.type.item.@bool.@this>("",
                           signature.Origin.Value == global::app.type.item.signature.Origin.Stored, context: action.Context) }, action.Context).Start(action.Context);
        if (!rehash.Success) return global::app.data.@this<global::app.type.item.@bool.@this>.From(rehash);
        if (await rehash.Value() is not global::app.module.crypto.type.hash.@this rehashValue || !rehashValue.DigestEquals(storedHash))
            return action.Context.Error<global::app.type.item.@bool.@this>(new ActionError("Data hash does not match signed hash", "DataHashMismatch", 400));

        // 5. Signature verification — over the signature's canonical signing bytes.
        if (signature.Signature.Value.Length == 0)
            return action.Context.Error<global::app.type.item.@bool.@this>(new ActionError("Missing signature", "SignatureInvalid", 400));

        try
        {
            if (!Verify(signature).Value)
                return action.Context.Error<global::app.type.item.@bool.@this>(new ActionError("Signature verification failed", "SignatureInvalid", 400));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException
            or System.Security.Cryptography.CryptographicException or InvalidOperationException)
        {
            return action.Context.Error<global::app.type.item.@bool.@this>(ActionError.FromException(ex, "SignatureInvalid", 400));
        }

        // 6. The verified live wire records its nonce until the signature expires — atomically, so of two
        // concurrent reads of one wire only the first verifies. After it expires, the expiry refuses it.
        if (live)
        {
            var now = await (await action.Context.Variable.Get("NowUtc")).Clr<DateTimeOffset>(DateTimeOffset.UtcNow);
            var remembered = (long)Math.Max(1, (signature.Expires!.Value - now).TotalMilliseconds);
            if (!await app.Cache.TryAddAsync(nonceCacheKey, action.Context.Ok(true), new CacheSettings { DurationMs = remembered }))
                return action.Context.Error<global::app.type.item.@bool.@this>(new ActionError("Nonce has already been used", "NonceReplay", 400));
        }
        return action.Context.Ok<global::app.type.item.@bool.@this>(true);
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

    public (KeyPair? keys, global::app.error.Error? error) GenerateKeyPair()
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

            // A public key is written URL-safe (- and _, no =): it is %Identity%, used as it is as a path
            // segment, a URL part or a file name.
            return (new KeyPair(
                System.Buffers.Text.Base64Url.EncodeToString(publicKeyBytes),
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
    public global::app.type.item.binary.@this Sign(global::app.type.item.signature.@this unsigned, global::app.type.item.text.@this privateKey)
    {
        var algorithm = SignatureAlgorithm.Ed25519;
        var privateKeyBytes = Convert.FromBase64String(privateKey.ToString());
        using var key = Key.Import(algorithm, privateKeyBytes, KeyBlobFormat.RawPrivateKey,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
        return new global::app.type.item.binary.@this(algorithm.Sign(key, unsigned.ToSigningBytes()));
    }

    public global::app.type.item.@bool.@this Verify(global::app.type.item.signature.@this signature)
    {
        var algorithm = SignatureAlgorithm.Ed25519;
        // the signer's key as it was written — URL-safe, or standard base64 from before keys were
        var publicKeyBytes = System.Buffers.Text.Base64Url.DecodeFromChars(
            signature.Identity.ToString().Replace('+', '-').Replace('/', '_').TrimEnd('='));
        var nsecPublicKey = NSec.Cryptography.PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);
        return new global::app.type.item.@bool.@this(algorithm.Verify(nsecPublicKey, signature.ToSigningBytes(), signature.Signature.Value));
    }
}
