using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nethereum.Util;
using app.error;
using app.variable;

namespace app.module.crypto.code;

public class Default : ICrypto
{
    public string Name => "default";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    // Context-less fallback — single source of truth lives on plang.@this so
    // Transport.Compress and crypto.Hash never drift. Used by Hash when called
    // outside of an actor scope (test fixtures, raw ICrypto consumers);
    // production goes through the registered serializer.
    private static global::app.channel.serializer.plang.@this _fallbackPlang
        => global::app.channel.serializer.plang.@this.ContextLessFallback;

    public async Task<data.@this<global::app.module.crypto.type.hash.@this>> Hash(Hash action)
    {
        var data = action.Data;
        byte[] bytes;
        var value = await data.Value();
        if (value is byte[] raw)
        {
            bytes = raw;
        }
        else if (value is global::app.type.binary.@this bin)
        {
            bytes = bin.Value;
        }
        else
        {
            // Canonicalize through the same wire options the merged application/plang
            // serializer uses, so hashed-bytes ≡ wire-bytes (minus the outermost
            // Signature field, suppressed via Wire.MarkOuterForHash).
            // The outer signature transitively binds inner Datas' signatures.
            //
            // If something other than the canonical plang.@this is registered for
            // "application/plang" (custom transport, test double, future format),
            // fail loud — hash and wire would diverge silently and signature
            // verification would behave inconsistently across the same payload.
            var registered = action.Context?.Actor?.Channel.Serializers.GetByType("application/plang");
            if (registered != null && registered is not global::app.channel.serializer.plang.@this)
                return global::app.data.@this<global::app.module.crypto.type.hash.@this>.FromError(new ActionError(
                    "Registered application/plang serializer is not the canonical plang.@this; hash bytes would diverge from wire bytes.",
                    "SerializerMismatch", 500));
            var serializer = (registered as global::app.channel.serializer.plang.@this) ?? _fallbackPlang;
            using (global::app.data.Wire.MarkOuterForHash(data))
            {
                bytes = JsonSerializer.SerializeToUtf8Bytes(data, serializer.OutboundOptions);
            }
        }
        string algorithm = (await action.Algorithm.Value())!.ToString()!.ToLowerInvariant();
        byte[]? hashBytes = algorithm switch
        {
            "keccak256" => new Sha3Keccack().CalculateHash(bytes),
            "sha256" => SHA256.HashData(bytes),
            _ => null
        };

        if (hashBytes == null)
            return global::app.data.@this<global::app.module.crypto.type.hash.@this>.FromError(new ActionError($"Algorithm '{action.Algorithm.Peek()}' is not supported", "UnsupportedAlgorithm", 400));

        // The value IS a hash (a digest that knows its algorithm), not bare
        // bytes — so the builder annotates the write-to variable as `%x% (hash)`
        // and the live serializer renders the digest. The algorithm is the
        // value's KIND; stamp {name: hash, kind: <algorithm>} so verify reads
        // the algorithm off the value instead of a loose, mismatch-prone param.
        return global::app.data.@this<global::app.module.crypto.type.hash.@this>.Ok(new global::app.module.crypto.type.hash.@this(hashBytes, algorithm),
            global::app.type.@this.Create("hash", kind: algorithm));
    }

    public async Task<data.@this<global::app.type.@bool.@this>> Verify(Verify action)
    {
        // The expected hash and its algorithm. The digest's own kind is
        // authoritative — a sha256 hash can only be verified by recomputing
        // sha256. When `%hash%` binds an actual hash value, the algorithm rides
        // on it (no separate parameter); when it's a bare base64 string, the
        // kind on the Type (if any) or the Algorithm parameter supplies it.
        global::app.module.crypto.type.hash.@this expected;
        string algorithm;
        if (await action.Hash.Value() is global::app.module.crypto.type.hash.@this bound)
        {
            expected = bound;
            algorithm = bound.Algorithm;
        }
        else
        {
            var hashKind = action.Hash.Type is { Name: "hash", Kind: { Length: > 0 } k } ? k : null;
            algorithm = hashKind ?? (await action.Algorithm.Value())!;
            // The hash type owns base64↔byte parsing (OBP) — Verify doesn't
            // reach for Convert.FromBase64String / SequenceEqual itself.
            try { expected = global::app.module.crypto.type.hash.@this.FromBase64((await action.Hash.Value())?.ToString() ?? "", algorithm); }
            catch (FormatException) { return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ActionError("Hash string is not valid base64", "InvalidHash", 400)); }
        }

        // Recompute through crypto.hash so the algorithm switch stays in one
        // place (no forked digest logic here).
        var hashResult = await Hash(new Hash
        {
            Context = action.Context,
            Data = action.Data,
            Algorithm = new global::app.data.@this<global::app.type.text.@this>("Algorithm", algorithm),
        });
        if (!hashResult.Success) return global::app.data.@this<global::app.type.@bool.@this>.FromError(hashResult.Error!);

        return global::app.data.@this<global::app.type.@bool.@this>.Ok(((global::app.module.crypto.type.hash.@this)(await hashResult.Value())!).DigestEquals(expected));
    }
}
