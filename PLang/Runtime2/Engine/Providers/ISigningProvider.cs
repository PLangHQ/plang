namespace PLang.Runtime2.Engine.Providers;

/// <summary>
/// Provider that can sign and verify data. Extends IKeyProvider for key generation.
/// </summary>
public interface ISigningProvider : IKeyProvider
{
    byte[] Sign(byte[] data, string privateKey);
    bool Verify(byte[] data, byte[] signature, string publicKey);
}
