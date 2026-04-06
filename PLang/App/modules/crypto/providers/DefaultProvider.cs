using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nethereum.Util;
using App.Errors;
using App.Variables;

namespace App.modules.crypto.providers;

public class DefaultCryptoProvider : ICryptoProvider
{
    public string Name => "default";
    public bool IsDefault { get; set; }

    public Data.@this Hash(Hash action)
    {
        var value = action.Data.Value;
        var bytes = value is byte[] raw ? raw : Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value));
        var algorithm = action.Algorithm.ToLowerInvariant();
        byte[]? hashBytes = algorithm switch
        {
            "keccak256" => new Sha3Keccack().CalculateHash(bytes),
            "sha256" => SHA256.HashData(bytes),
            _ => null
        };

        if (hashBytes == null)
            return Data.@this.FromError(new ActionError($"Algorithm '{action.Algorithm}' is not supported", "UnsupportedAlgorithm", 400));

        return Data.@this.Ok(hashBytes, Data.Type.FromName(algorithm));
    }

    public Data.@this Verify(Verify action)
    {
        byte[] expectedHash;
        try { expectedHash = Convert.FromBase64String(action.Hash); }
        catch (FormatException) { return Data.@this.FromError(new ActionError("Hash string is not valid base64", "InvalidHash", 400)); }

        var hashResult = Hash(new Hash { Context = action.Context, Data.@this = action.Data, Algorithm = action.Algorithm });
        if (!hashResult.Success) return hashResult;

        return Data.@this.Ok(((byte[])hashResult.Value!).AsSpan().SequenceEqual(expectedHash));
    }
}
