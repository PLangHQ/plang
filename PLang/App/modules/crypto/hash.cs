using App.Variables;
using App.modules.crypto.code;

namespace App.modules.crypto;

[ModuleDescription("Cryptographic hashing and hash verification using configurable algorithms")]
[System.ComponentModel.Description("Hash data using the specified algorithm (default keccak256) and return the hex digest")]
[Example("hash %content%, write to %hash%",
    "crypto.hash Data([object] %content%), Algorithm([string] keccak256) | variable.set Name([string] %hash%), Value([object] %__data__%)")]
[Action("hash", Cacheable = false)]
public partial class Hash : IContext
{
    [IsNotNull]
    public partial Data.@this Data { get; init; }

    [Default("keccak256")]
    public partial Data.@this<string> Algorithm { get; init; }

    [Provider]
    public partial ICrypto Crypto { get; }

    public async Task<Data.@this> Run() => Crypto.Hash(this);
}
