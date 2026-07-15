using app.variable;
using app.module.action.crypto.code;

namespace app.module.action.crypto;

[Action("hash", Cacheable = false)]
public partial class Hash : IContext
{
    [IsNotNull]
    public partial data.@this Data { get; init; }

    [Default("keccak256")]
    public partial data.@this<global::app.type.item.text.@this> Algorithm { get; init; }

    /// <summary>
    /// Canonicalize the value in the Store view (all <c>[Store]</c> fields) rather than the
    /// default Out view. The hash must be taken in the SAME view the data is serialized in:
    /// a stored value's wire-reconstruction is a property-bag carrying every <c>[Store]</c>
    /// field, so re-hashing it in Out view (a subset) diverges from the typed value's Out
    /// hash. Sign and verify both pass the data's destination view here so the digest is
    /// over the exact bytes that cross the wire. Default false (Out, for transport).
    /// </summary>
    public partial data.@this<global::app.type.item.@bool.@this>? StoreView { get; init; }

    [Code]
    public partial ICrypto Crypto { get; }

    // Returns a hash value (not bytes) so the digest carries its algorithm as
    // the type kind: the builder annotates the write-to variable as `%x% (hash)`
    // for later steps, and crypto.verify reads the algorithm off the value.
    public async Task<data.@this<global::app.module.action.crypto.type.hash.@this>> Run() => await Crypto.Hash(this);
}
