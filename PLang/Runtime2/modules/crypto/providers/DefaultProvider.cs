using System.Security.Cryptography;
using Nethereum.Util;

namespace PLang.Runtime2.modules.crypto.providers;

public class DefaultProvider : ICryptoProvider
{
    public byte[] Hash(byte[] data, string algorithm)
    {
        return algorithm.ToLowerInvariant() switch
        {
            "keccak256" => new Sha3Keccack().CalculateHash(data),
            "sha256" => SHA256.HashData(data),
            _ => throw new NotSupportedException($"Algorithm '{algorithm}' is not supported")
        };
    }

    public bool Verify(byte[] data, byte[] expectedHash, string algorithm)
    {
        if (algorithm.ToLowerInvariant() == "keccak256" || algorithm.ToLowerInvariant() == "sha256")
        {
            var actual = Hash(data, algorithm);
            return actual.AsSpan().SequenceEqual(expectedHash);
        }

        throw new NotSupportedException($"Algorithm '{algorithm}' is not supported");
    }
}
