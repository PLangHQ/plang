using app.Variables;
using app.modules.crypto.code;

namespace app.modules.crypto;

[ModuleDescription("Cryptographic hashing and hash verification using configurable algorithms")]
[System.ComponentModel.Description("Hash data using the specified algorithm (default keccak256) and return the hex digest")]
[Example("hash %content%, write to %hash%",
    "crypto.hash Data([object] %content%), Algorithm([string] keccak256) | variable.set Name([string] %hash%), Value([object] %__data__%)")]
[Action("hash", Cacheable = false)]
public partial class Hash : IContext
{
    [IsNotNull]
    public partial data.@this Data { get; init; }

    [Default("keccak256")]
    public partial data.@this<string> Algorithm { get; init; }

    [Code]
    public partial ICrypto Crypto { get; }

    public async Task<data.@this> Run() => Crypto.Hash(this);
}
