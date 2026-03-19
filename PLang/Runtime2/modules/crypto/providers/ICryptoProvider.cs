namespace PLang.Runtime2.modules.crypto.providers;

public interface ICryptoProvider
{
    byte[] Hash(byte[] data, string algorithm);
    bool Verify(byte[] data, byte[] expectedHash, string algorithm);
}
