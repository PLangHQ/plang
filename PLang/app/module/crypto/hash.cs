using app.module.crypto.code;

namespace app.module.crypto;

[Action("hash", Cacheable = false)]
public partial class Hash : IContext
{
    /// <summary>The value to hash — its own bytes: a text its UTF-8, binary its bytes, a path the file's contents (read as
    /// they stream, gated as a read), anything else its json text (a dict or list in its own key order), so the digest
    /// matches any other tool's. A text naming a file is still a text: its letters.</summary>
    [IsNotNull, Whole]
    public partial data.@this Data { get; init; }

    /// <summary>The kind of hash — one of the hash kinds (sha256, keccak256).</summary>
    [Default("keccak256")]
    public partial data.@this<global::app.type.item.choice.@this<global::app.module.crypto.type.hash.kind.@this>> Algorithm { get; init; }

    [Code]
    public partial ICrypto Crypto { get; }

    // Returns a hash value (not bytes) so the digest carries its algorithm as
    // the type kind: the builder annotates the write-to variable as `%x% (hash)`
    // for later steps, and crypto.verify reads the algorithm off the value.
    public async Task<data.@this<global::app.module.crypto.type.hash.@this>> Start() => await Crypto.Hash(this);
}
