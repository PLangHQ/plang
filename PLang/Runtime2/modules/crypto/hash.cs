using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.crypto.providers;

namespace PLang.Runtime2.modules.crypto;

[Action("hash", Cacheable = false)]
public partial class Hash : IContext
{
    [IsNotNull]
    public partial Data Data { get; init; }

    [Default("keccak256")]
    public partial string Algorithm { get; init; }

    [Provider]
    public partial ICryptoProvider Crypto { get; }

    public async Task<Data> Run() => Crypto.Hash(this);
}
