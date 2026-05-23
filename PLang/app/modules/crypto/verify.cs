using app.variables;
using app.modules.crypto.code;

namespace app.modules.crypto;

[System.ComponentModel.Description("Verify that Data matches a previously computed Hash using the specified algorithm")]
[Example("verify %content% against %hash%, write to %isValid%",
    "crypto.verify Data([object] %content%), Hash([string] %hash%), Algorithm([string] keccak256) | variable.set Name([string] %isValid%), Value([object] %!data%)")]
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
