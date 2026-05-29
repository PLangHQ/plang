using app.variable;
using app.modules.crypto.code;

namespace app.modules.crypto;

[Action("verify", Cacheable = false)]
public partial class Verify : IContext
{
    [IsNotNull]
    public partial data.@this Data { get; init; }

    [IsNotNull]
    public partial data.@this<string> Hash { get; init; }

    [Default("keccak256")]
    public partial data.@this<string> Algorithm { get; init; }

    [Code]
    public partial ICrypto Crypto { get; }

    public async Task<data.@this<bool>> Run() => Crypto.Verify(this);
}
