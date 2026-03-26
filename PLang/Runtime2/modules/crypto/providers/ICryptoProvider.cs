using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

namespace PLang.Runtime2.modules.crypto.providers;

public interface ICryptoProvider : IProvider
{
    Data Hash(Hash action);
    Data Verify(Verify action);
}
