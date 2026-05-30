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

    public data.@this<byte[]> Hash(Hash action)
    {
        var data = action.Data;
        byte[] bytes;
        if (data.Value is byte[] raw)
        {
            bytes = raw;
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
                return global::app.data.@this<byte[]>.FromError(new ActionError(
                    "Registered application/plang serializer is not the canonical plang.@this; hash bytes would diverge from wire bytes.",
                    "SerializerMismatch", 500));
            var serializer = (registered as global::app.channel.serializer.plang.@this) ?? _fallbackPlang;
            using (global::app.data.Wire.MarkOuterForHash(data))
            {
                bytes = JsonSerializer.SerializeToUtf8Bytes(data, serializer.OutboundOptions);
            }
        }
        var algorithm = action.Algorithm.Value!.ToLowerInvariant();
        byte[]? hashBytes = algorithm switch
        {
            "keccak256" => new Sha3Keccack().CalculateHash(bytes),
            "sha256" => SHA256.HashData(bytes),
            _ => null
        };

        if (hashBytes == null)
            return global::app.data.@this<byte[]>.FromError(new ActionError($"Algorithm '{action.Algorithm.Value}' is not supported", "UnsupportedAlgorithm", 400));

        // The value is the digest bytes; the algorithm is the value's KIND, not
        // its name. Stamp {name: hash, kind: <algorithm>} so the digest knows
        // how it was produced — verify reads the algorithm off the kind instead
        // of taking it as a loose, mismatch-prone parameter.
        return global::app.data.@this<byte[]>.Ok(hashBytes,
            global::app.type.@this.Create("hash", kind: algorithm));
    }

    public data.@this<bool> Verify(Verify action)
    {
        // The digest's own kind is authoritative — a sha256 hash can only be
        // verified by recomputing sha256. When the expected-hash value carries
        // {name: hash, kind: <algorithm>} (As<T> preserves the source Type),
        // use that algorithm; otherwise fall back to the Algorithm parameter
        // (a bare base64 string carries no kind). So `verify %data% against
        // %hash%` needs no separate algorithm.
        var hashKind = action.Hash.Type is { Name: "hash", Kind: { Length: > 0 } k } ? k : null;
        var algorithm = hashKind ?? action.Algorithm.Value!;

        // The hash type owns base64↔byte parsing (OBP) — Verify doesn't reach
        // for Convert.FromBase64String / SequenceEqual itself.
        global::app.type.hash.@this expected;
        try { expected = global::app.type.hash.@this.FromBase64(action.Hash.Value!, algorithm); }
        catch (FormatException) { return global::app.data.@this<bool>.FromError(new ActionError("Hash string is not valid base64", "InvalidHash", 400)); }

        var hashResult = Hash(new Hash
        {
            Context = action.Context,
            Data = action.Data,
            Algorithm = new global::app.data.@this<string>("Algorithm", algorithm),
        });
        if (!hashResult.Success) return global::app.data.@this<bool>.FromError(hashResult.Error!);

        var recomputed = new global::app.type.hash.@this((byte[])hashResult.Value!, algorithm);
        return global::app.data.@this<bool>.Ok(recomputed.DigestEquals(expected));
    }
}
