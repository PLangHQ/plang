using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.crypto.providers;

public interface ICryptoProvider
{
    Data Hash(byte[] data, string algorithm);
    Data Verify(byte[] data, byte[] expectedHash, string algorithm);
}
