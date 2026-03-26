using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nethereum.Util;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.crypto.providers;

public class DefaultCryptoProvider : ICryptoProvider
{
    public string Name => "default";
    public bool IsDefault { get; set; }

    public Data Hash(Hash action)
    {
        var bytes = action.Data is byte[] raw ? raw : Encoding.UTF8.GetBytes(JsonSerializer.Serialize(action.Data));
        return action.Algorithm.ToLowerInvariant() switch
        {
            "keccak256" => Data.Ok(new Sha3Keccack().CalculateHash(bytes)),
            "sha256" => Data.Ok(SHA256.HashData(bytes)),
            _ => Data.FromError(new ActionError($"Algorithm '{action.Algorithm}' is not supported", "UnsupportedAlgorithm", 400))
        };
    }

    public Data Verify(Verify action)
    {
        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromBase64String(action.Hash);
        }
        catch (FormatException)
        {
            return Data.FromError(new ActionError("Hash string is not valid base64", "InvalidHash", 400));
        }

        // Re-hash using the same serialization path
        var hashAction = new Hash { Context = action.Context, Data = action.Data, Algorithm = action.Algorithm };
        var hashResult = Hash(hashAction);
        if (!hashResult.Success) return hashResult;

        var actual = (byte[])hashResult.Value!;
        return Data.Ok(actual.AsSpan().SequenceEqual(expectedHash));
    }
}
