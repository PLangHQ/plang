using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nethereum.Util;
using app.errors;
using app.variables;

namespace app.modules.crypto.code;

public class Default : ICrypto
{
    public string Name => "default";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    // Context-less fallback wire options — same shape as the registered
    // application/plang serializer but without an actor-bound path converter.
    // Used by Hash when called outside of an actor scope (test fixtures, raw
    // ICrypto consumers); production goes through the registered serializer.
    private static readonly global::app.channels.serializers.serializer.plang.@this _fallbackPlang =
        new global::app.channels.serializers.serializer.plang.@this();

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
            // Signature field, suppressed via WireJsonConverter.MarkOuterForHash).
            // The outer signature transitively binds inner Datas' signatures.
            var serializer = action.Context?.Actor?.Channels.Serializers.GetByType("application/plang")
                             as global::app.channels.serializers.serializer.plang.@this
                             ?? _fallbackPlang;
            using (global::app.data.WireJsonConverter.MarkOuterForHash(data))
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

        return global::app.data.@this<byte[]>.Ok(hashBytes, global::app.data.type.FromName(algorithm));
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
