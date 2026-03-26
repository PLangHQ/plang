using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.crypto.providers;

namespace PLang.Runtime2.modules.crypto;

/// PLang: - verify %content% against %hash%, write to %isValid%
/// PLang: - verify %data% against %hash% with sha256, write to %isValid%
[Action("verify", Cacheable = false)]
public partial class Verify : IContext
{
    [IsNotNull]
    public partial Data Data { get; init; }

    [IsNotNull]
    public partial string Hash { get; init; }

    [Default("keccak256")]
    public partial string Algorithm { get; init; }

    [Provider]
    public partial ICryptoProvider Crypto { get; }

    public async Task<Data> Run() => Crypto.Verify(this);
}
