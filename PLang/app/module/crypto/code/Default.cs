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
        // The value's own bytes, read through its door (a %variable% hashes what it holds): a text its UTF-8, binary
        // its bytes, anything else its json text — so a digest matches any other tool's. A signature's digest is the
        // Data's wire bytes instead (data.Digest), never this.
        var value = await data.Value();
        if (!data.Success)
            return action.Context.Error<global::app.module.crypto.type.hash.@this>(data.Error!);
        byte[] bytes;
        if (value is global::app.type.item.binary.@this bin) bytes = bin.Value;
        else if (value is global::app.type.item.text.@this text) bytes = Encoding.UTF8.GetBytes(text.ToString());
        else
        {
            // a dict or list is its json text in its own key order (insertion): the same entries added in another
            // order give another digest
            using var json = new MemoryStream();
            await using (var utf8 = new System.Text.Json.Utf8JsonWriter(json))
                await value.Output(new global::app.type.item.kind.json.Writer(utf8, global::app.View.Out, emitsSchema: false),
                    global::app.View.Out, action.Context);
            bytes = json.ToArray();
        }

        string algorithm = (await action.Algorithm.Value())!.ToString()!.ToLowerInvariant();
        // The value IS a hash (a digest that knows its algorithm), stamped {name: hash, kind: <algorithm>}, so the
        // builder annotates the write-to as `%x% (hash)` and verify reads the algorithm off the value.
        if (global::app.module.crypto.type.hash.@this.Of(bytes, algorithm, action.Context) is not { } hash)
            return action.Context.Error<global::app.module.crypto.type.hash.@this>(new ActionError($"Algorithm '{action.Algorithm.Peek()}' is not supported", "UnsupportedAlgorithm", 400));
        return action.Context.Ok<global::app.module.crypto.type.hash.@this>(hash,
            action.Context.App.type.list[new global::app.type.@this("hash", algorithm), action.Context]);
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

        global::app.module.crypto.type.hash.@this expected;
        string algorithm;
        if (await action.Hash.Value() is global::app.module.crypto.type.hash.@this bound)
        {
            expected = bound;
            algorithm = bound.Algorithm;
        }
        else
        {
            var hashKind = action.Hash.Type is { Name: "hash", kind: { IsEmpty: false } k } ? k.Name : null;
            algorithm = hashKind ?? (await action.Algorithm.Value())!.Clr<string>()!;
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
            Algorithm = new global::app.data.@this<global::app.type.item.text.@this>("Algorithm", algorithm, context: action.Context),
        });
        if (!hashResult.Success) return action.Context.Error<global::app.type.item.@bool.@this>(hashResult.Error!);

        return action.Context.Ok<global::app.type.item.@bool.@this>(((global::app.module.crypto.type.hash.@this)(await hashResult.Value())!).DigestEquals(expected));
    }
}
