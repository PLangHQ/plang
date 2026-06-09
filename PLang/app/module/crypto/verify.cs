using app.variable;
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

    [Default("keccak256")]
    public partial data.@this<global::app.type.text.@this> Algorithm { get; init; }

    [Code]
    public partial ICrypto Crypto { get; }

    public async Task<data.@this<global::app.type.@bool.@this>> Run() => await Crypto.Verify(this);
}
