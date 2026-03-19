using System.Security.Cryptography;
using Nethereum.Util;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.crypto.providers;

/// <summary>
/// Built-in crypto provider. Supports keccak256 (via Nethereum) and sha256 (via System.Security.Cryptography).
/// Returns unsupported-algorithm errors as <c>Data.FromError</c> — never throws.
/// </summary>
public class DefaultProvider : ICryptoProvider
{
    public Data Hash(byte[] data, string algorithm)
    {
        return algorithm.ToLowerInvariant() switch
        {
            "keccak256" => Data.Ok(new Sha3Keccack().CalculateHash(data)),
            "sha256" => Data.Ok(SHA256.HashData(data)),
            _ => Data.FromError(new ActionError($"Algorithm '{algorithm}' is not supported", "UnsupportedAlgorithm", 400))
        };
    }

    public Data Verify(byte[] data, byte[] expectedHash, string algorithm)
    {
        var hashResult = Hash(data, algorithm);
        if (!hashResult.Success)
            return hashResult;

        var actual = (byte[])hashResult.Value!;
        return Data.Ok(actual.AsSpan().SequenceEqual(expectedHash));
    }
}
