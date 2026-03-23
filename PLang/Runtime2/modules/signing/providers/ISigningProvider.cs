using PLang.Runtime2.Engine.Memory;

using PLang.Runtime2.Engine.Providers;

namespace PLang.Runtime2.modules.signing.providers;

/// <summary>
/// Provider that can sign and verify data. Extends IKeyProvider for key generation.
/// </summary>
public interface ISigningProvider : IKeyProvider
{
    /// <summary>Signs data with the given private key. Returns signature bytes on success.</summary>
    /// <param name="data">The bytes to sign (typically deterministic JSON from SignedData.ToSigningBytes).</param>
    /// <param name="privateKey">Base64-encoded private key.</param>
    Data Sign(byte[] data, string privateKey);

    /// <summary>Verifies a signature against the original data and public key.</summary>
    /// <param name="data">The original bytes that were signed.</param>
    /// <param name="signature">The signature bytes to verify.</param>
    /// <param name="publicKey">Base64-encoded public key of the signer.</param>
    Data Verify(byte[] data, byte[] signature, string publicKey);
}
