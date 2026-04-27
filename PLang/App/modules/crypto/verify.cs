using App.Variables;
using App.modules.crypto.providers;

namespace App.modules.crypto;

[System.ComponentModel.Description("Verify that Data matches a previously computed Hash using the specified algorithm")]
[Example("verify %content% against %hash%, write to %isValid%",
    "crypto.verify Data([object] %content%), Hash([string] %hash%), Algorithm([string] keccak256) | variable.set Name([string] %isValid%), Value([object] %__data__%)")]
[Action("verify", Cacheable = false)]
public partial class Verify : IContext
{
    [IsNotNull]
    public partial Data.@this Data { get; init; }

    [IsNotNull]
    public partial Data.@this<string> Hash { get; init; }

    [Default("keccak256")]
    public partial Data.@this<string> Algorithm { get; init; }

    [Provider]
    public partial ICryptoProvider Crypto { get; }

    public async Task<Data.@this> Run() => Crypto.Verify(this);
}
