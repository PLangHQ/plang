using app.module.crypto.code;

namespace app.module.crypto;

[Action("verify", Cacheable = false)]
public partial class Verify : IContext
{
    [IsNotNull]
    public partial data.@this Data { get; init; }

    // The expected hash — either a hash value (carrying its algorithm as kind)
    // or a bare base64 string. Untyped so a bound `hash` value survives intact;
    // a string with no kind falls back to the Algorithm parameter.
    [IsNotNull]
    public partial data.@this Hash { get; init; }

    /// <summary>The kind of hash, when the expected hash doesn't carry one — one of the hash kinds (sha256, keccak256).</summary>
    [Default("keccak256")]
    public partial data.@this<global::app.type.item.choice.@this<global::app.module.crypto.type.hash.kind.@this>> Algorithm { get; init; }

    [Code]
    public partial ICrypto Crypto { get; }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Start() => await Crypto.Verify(this);
}
