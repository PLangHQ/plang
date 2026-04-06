using App.Variables;
using App.modules.crypto.providers;

namespace App.modules.crypto;

[Example("verify %content% against %hash%, write to %isValid%", "Data=%content%, Hash=%hash%, Algorithm=keccak256")]
[Example("verify %data% against %hash% with sha256, write to %isValid%", "Data=%data%, Hash=%hash%, Algorithm=sha256")]
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
