using App.Engine.Variables;
using App.modules.crypto.providers;

namespace App.modules.crypto;

[Example("hash %content%, write to %hash%", "Data=%content%, Algorithm=keccak256")]
[Example("hash %data% with sha256, write to %hash%", "Data=%data%, Algorithm=sha256")]
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
