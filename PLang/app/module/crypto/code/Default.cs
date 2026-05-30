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

        return global::app.data.@this<byte[]>.Ok(hashBytes, global::app.type.@this.FromName(algorithm));
    }

    public data.@this<bool> Verify(Verify action)
    {
        byte[] expectedHash;
        try { expectedHash = Convert.FromBase64String(action.Hash.Value!); }
        catch (FormatException) { return global::app.data.@this<bool>.FromError(new ActionError("Hash string is not valid base64", "InvalidHash", 400)); }

        var hashResult = Hash(new Hash { Context = action.Context, Data = action.Data, Algorithm = action.Algorithm });
        if (!hashResult.Success) return global::app.data.@this<bool>.FromError(hashResult.Error!);

        return global::app.data.@this<bool>.Ok(((byte[])hashResult.Value!).AsSpan().SequenceEqual(expectedHash));
    }
}
