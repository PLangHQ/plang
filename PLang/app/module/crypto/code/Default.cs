using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nethereum.Util;
using app.error;

namespace app.module.crypto.code;

public class Default : ICrypto
{
    public string Name => "default";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public async Task<data.@this<global::app.module.crypto.type.hash.@this>> Hash(Hash action)
    {
        var data = action.Data;
        // A value left out (or null) has nothing to hash. Presence, not truthiness — 0, false and "" are values with
        // digests.
        if (data.Error == null && !data.HasValue)
            return action.Context.Error<global::app.module.crypto.type.hash.@this>(new ActionError(
                "Hash requires a value to hash", "ValueRequired", 400));
        // The value, read through its door (a %variable% hashes what it holds). A signature's digest is the Data's
        // wire bytes instead (data.Digest), never this.
        var value = await data.Value();
        if (!data.Success)
            return action.Context.Error<global::app.module.crypto.type.hash.@this>(data.Error!);

        // the kind of hash chosen; a name that is no kind of hash is the choice's own refusal, naming the kinds
        if (await action.Algorithm.Value() is not { } chosen)
            return global::app.data.@this<global::app.module.crypto.type.hash.@this>.From(action.Algorithm);
        // The value pours its own bytes into the kind's digest — a text its UTF-8, binary its bytes, a file its contents
        // as they stream (gated as a read), anything else its json text in its own key order — so a digest matches any
        // other tool's. The value IS then a hash (a digest that knows its algorithm), stamped {name: hash, kind:
        // <algorithm>}, so the builder annotates the write-to as `%x% (hash)` and verify reads the algorithm off it.
        using var digest = chosen.Value.Digest();
        var poured = await value!.Pour(digest, action.Context);
        if (!poured.Success) return global::app.data.@this<global::app.module.crypto.type.hash.@this>.From(poured);
        var hash = digest.Hash;
        return action.Context.Ok<global::app.module.crypto.type.hash.@this>(hash,
            action.Context.App.type.list[new global::app.type.@this("hash", hash.Algorithm?.Name), action.Context]);
    }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Verify(Verify action)
    {
        // The expected hash and its algorithm. The digest's own kind is
        // authoritative — a sha256 hash can only be verified by recomputing
        // sha256. When `%hash%` binds an actual hash value, the algorithm rides
        // on it (no separate parameter); when it's a bare base64 string, the
        // kind on the Type (if any) or the Algorithm parameter supplies it.
        // The value under verification must exist before we bother parsing the
        // expected-hash string — a payload left out (or null) is a missing input, not a bad hash.
        if (!action.Data.HasValue)
            return action.Context.Error<global::app.type.item.@bool.@this>(new ActionError(
                "Verify requires a value to verify", "ValueRequired", 400));

        // the kind of hash to check with: the bound hash's own, else the kind its type declares, else Algorithm
        global::app.module.crypto.type.hash.@this expected;
        global::app.module.crypto.type.hash.kind.@this algorithm;
        if (await action.Hash.Value() is global::app.module.crypto.type.hash.@this { Algorithm: { } own } bound)
        {
            expected = bound;
            algorithm = own;
        }
        else
        {
            if (await action.Algorithm.Value() is not { } chosen)
                return global::app.data.@this<global::app.type.item.@bool.@this>.From(action.Algorithm);
            algorithm = action.Hash.Type is { Name: "hash", kind: { IsEmpty: false } k }
                && action.Context.App.type.list["hash"].kind[k.Name] is global::app.module.crypto.type.hash.kind.@this declared
                ? declared : chosen.Value;
            // The hash type owns base64↔byte parsing (OBP) — Verify doesn't
            // reach for Convert.FromBase64String / SequenceEqual itself.
            try { expected = global::app.module.crypto.type.hash.@this.FromBase64((await action.Hash.Value())?.ToString() ?? "", algorithm); }
            catch (FormatException) { return action.Context.Error<global::app.type.item.@bool.@this>(new ActionError("Hash string is not valid base64", "InvalidHash", 400)); }
        }

        // Recompute through crypto.hash so the algorithm switch stays in one
        // place (no forked digest logic here).
        var hashResult = await Hash(new Hash(action.Context)
        {
            Data = action.Data,
            Algorithm = (global::app.type.item.choice.@this<global::app.module.crypto.type.hash.kind.@this>)algorithm,
        });
        if (!hashResult.Success) return action.Context.Error<global::app.type.item.@bool.@this>(hashResult.Error!);

        return action.Context.Ok<global::app.type.item.@bool.@this>(((global::app.module.crypto.type.hash.@this)(await hashResult.Value())!).DigestEquals(expected));
    }
}
