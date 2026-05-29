using app.variable;
using app.modules.crypto.code;

namespace app.modules.crypto;

[Action("hash", Cacheable = false)]
public partial class Hash : IContext
{
    [IsNotNull]
    public partial data.@this Data { get; init; }

    [Default("keccak256")]
    public partial data.@this<string> Algorithm { get; init; }

    [Code]
    public partial ICrypto Crypto { get; }

    public async Task<data.@this<byte[]>> Run() => Crypto.Hash(this);
}
