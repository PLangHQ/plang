using app.variable;
using app.module.crypto.code;

namespace app.module.crypto;

[Action("hash", Cacheable = false)]
public partial class Hash : IContext
{
    [IsNotNull]
    public partial data.@this Data { get; init; }

    [Default("keccak256")]
    public partial data.@this<global::app.type.text.@this> Algorithm { get; init; }

    [Code]
    public partial ICrypto Crypto { get; }

    // Returns a hash value (not bytes) so the digest carries its algorithm as
    // the type kind: the builder annotates the write-to variable as `%x% (hash)`
    // for later steps, and crypto.verify reads the algorithm off the value.
    public async Task<data.@this<global::app.module.crypto.type.hash.@this>> Run() => Crypto.Hash(this);
}
