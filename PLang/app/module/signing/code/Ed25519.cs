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
        // The app's identity, its key whole, from its owner (the identity setting) — never through identity.get, which
        // hands a step the identity without its key
        var (provider, missing) = action.Context.App.Code.Get<global::app.module.identity.code.IIdentity>();
        if (provider == null) return action.Context.Error(missing!);
        var identityResult = await provider.GetOrCreateDefaultAsync(new identity.Get(action.Context));
        if (!identityResult.Success) return identityResult;
        var identity = (Identity)(await identityResult.Value())!;

        // The data as the view it is signed in takes it, settled once — plang's own store as written, anything that
        // crosses a copy of what it names — and the signature hashes and holds that one Data: the variable changing
        // afterwards changes nothing it holds.
        var view = action.View;
        global::app.data.@this data;
        try { data = await action.Data!.Settle(view); }
        catch (global::app.error.AppException exception) { return action.Context.Error(exception.Error); }
        // The digest of the inner data as it crosses the wire, in that view — what binds the value into the signed
        // bytes — by the kind of hash the signing setting names.
        var hash = await data.Digest(view, (await action.Context.Setting.Get<global::app.module.signing.setting.@this>()).Hash.Value, action.Context);

        var now = await (await action.Context.Variable.Get("NowUtc")).Clr<DateTimeOffset>(default);
        var nonce = (await (await action.Context.Variable.Get("GUID")).Clr<Guid>(default)).ToString();
        DateTimeOffset? expires = (action.Expires == null ? null : await action.Expires.Value()) is { } expiry
            ? now.Add(expiry.Value) : null;
        var contracts = action.Contracts == null ? null : await action.Contracts.Value();

        // Build the unsigned signature wrapping the data, compute its signing bytes,
        // sign with the identity's private key, stamp the signature in.
        var datetime = action.Context.App.type.list[typeof(global::app.type.item.datetime.@this)];
        var text = action.Context.App.type.list[typeof(global::app.type.item.text.@this)];
        var unsigned = new global::app.type.item.signature.@this(
            type: action.Context.App.type.list[new global::app.type.@this("signature", Name), action.Context],
            value: data,
            algorithm: new global::app.type.item.text.@this(Name, text),
            nonce: new global::app.type.item.text.@this(nonce, text),
            created: new global::app.type.item.datetime.@this(now, datetime),
            identity: new global::app.type.item.text.@this(identity.PublicKey, text),
            hash: hash,
            signature: new global::app.type.item.binary.@this(Array.Empty<byte>(), action.Context.App.type.list[typeof(global::app.type.item.binary.@this)]),
            expires: expires is { } e ? new global::app.type.item.datetime.@this(e, datetime) : null,
            contracts: contracts,
            // the view it was hashed in, signed with it: verify re-hashes there, whatever carries it
            view: new global::app.type.item.text.@this(view.ToString().ToLowerInvariant(), text));

        try
        {
            // the Data it hashed, holding its layer — what a read off the wire answers too
            var signed = unsigned.Signed(Sign(unsigned, new global::app.type.item.text.@this(identity.PrivateKey, text)));
            return data.Copy(signed);
        }
        catch (Exception ex)
        {
            return action.Context.Error(ActionError.FromException(ex, "SigningError", 500));
        }
    }

    public virtual async Task<data.@this<global::app.type.item.@bool.@this>> VerifyAsync(verify action)
    {
        // what the step gives, as it is now: a %variable% is the signed Data it holds, holding its layer
        var bearer = action.Data == null ? null : await action.Data.Follow(action.Context);
        if (bearer?.Signature is not { } signature)
            return action.Context.Error<global::app.type.item.@bool.@this>(new ActionError("Data has no signature", "NoSignature", 400));

        var app = action.Context.App;

        // 1. Expiry — the signature answers it: what its signer signed, or, read live, its window after it was made.
        if ((await signature.Expired(action.Context)).Value)
            return action.Context.Error<global::app.type.item.@bool.@this>(new ActionError(
                $"Signature has expired (at {signature.Expires!.Value:O})", "Expired", 400));

        // 2. Contract matching — Contracts may be an unset/absent slot (the
        // boundary-verify path never sets it), so guard the resolved value too.
        var contractsList = action.Contracts == null ? null : await action.Contracts.Value();
        var expectedContracts = contractsList == null ? null
            : System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(
                contractsList.Items(action.Context), d => d.Peek().ToString() ?? ""));
        if (!ContractsMatch(System.Linq.Enumerable.ToList(signature.ContractStrings()), expectedContracts))
            return action.Context.Error<global::app.type.item.@bool.@this>(new ActionError("Contract mismatch", "ContractMismatch", 400));

        // 3. Data hash verification — rehash the Data that holds the signature, as it is now, compare to the
        // signed digest (which carries its own algorithm).
        var storedHash = signature.Hash;
        if (storedHash.Bytes.Length == 0)
            return action.Context.Error<global::app.type.item.@bool.@this>(new ActionError("Missing data hash", "DataHashMismatch", 400));
        if (storedHash.Algorithm is not { } kind)
            return action.Context.Error<global::app.type.item.@bool.@this>(new ActionError(
                "The signed hash names no kind — it can't be checked", "DataHashMismatch", 400));

        // Re-hash in the view the data was signed in, by the kind it was signed with: the view the signature records, or,
        // for one that records none, the view its origin says (a store read is a property-bag carrying every [Store]
        // field; hashing it in Out, a subset, would diverge).
        if (!(await signature.Over(bearer).Value.Digest(signature.Hashed, kind, action.Context)).DigestEquals(storedHash))
            return action.Context.Error<global::app.type.item.@bool.@this>(new ActionError("Data hash does not match signed hash", "DataHashMismatch", 400));

        // 4. Signature verification — over the signature's canonical signing bytes.
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

        // 5. Nonce replay — a replay is a wire arriving twice. A signature read live presents its nonce once, for the
        // arrival its read birthed: the first verify that passes the hash and signature records it until the
        // signature expires — atomically, so of two reads of one wire only the first arrives — and every copy of that
        // arrival shares the answer. One read from plang's own store re-presents the same nonce on every read, which
        // isn't replay. After it expires, the expiry refuses it.
        if (signature.Origin.Value == global::app.type.item.signature.Origin.Live
            && !await signature.Present(async () =>
            {
                var now = await (await action.Context.Variable.Get("NowUtc")).Clr<DateTimeOffset>(DateTimeOffset.UtcNow);
                var remembered = (long)Math.Max(1, (signature.Expires!.Value - now).TotalMilliseconds);
                return await app.Cache.TryAddAsync($"nonce:{signature.Nonce}", action.Context.Ok(true), new CacheSettings { DurationMs = remembered });
            }))
            return action.Context.Error<global::app.type.item.@bool.@this>(new ActionError("Nonce has already been used", "NonceReplay", 400));
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

    public (KeyPair? keys, global::app.error.@this? error) GenerateKeyPair()
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
        return new global::app.type.item.binary.@this(algorithm.Sign(key, unsigned.ToSigningBytes()), unsigned.Signature.Type);
    }

    public global::app.type.item.@bool.@this Verify(global::app.type.item.signature.@this signature)
    {
        var algorithm = SignatureAlgorithm.Ed25519;
        // the signer's key as it signed it: an identity's key is URL-safe and unpadded from birth, so a key in any other
        // form is no signer's and fails to decode (refused as SignatureInvalid) — padded too, which the decoder would take
        var identity = signature.Identity.ToString();
        if (identity.Contains('='))
            throw new FormatException($"the signer's key '{identity}' is padded: an identity's key is written without padding");
        var publicKeyBytes = System.Buffers.Text.Base64Url.DecodeFromChars(identity);
        var nsecPublicKey = NSec.Cryptography.PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);
        return algorithm.Verify(nsecPublicKey, signature.ToSigningBytes(), signature.Signature.Value);
    }
}
