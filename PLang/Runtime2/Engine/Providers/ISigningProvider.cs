using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.Providers;

/// <summary>
/// Provider that can sign and verify data. Extends IKeyProvider for key generation.
/// </summary>
public interface ISigningProvider : IKeyProvider
{
    Data Sign(byte[] data, string privateKey);
    Data Verify(byte[] data, byte[] signature, string publicKey);
}
