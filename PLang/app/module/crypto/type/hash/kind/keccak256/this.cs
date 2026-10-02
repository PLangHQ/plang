namespace app.module.crypto.type.hash.kind.keccak256;

/// <summary>The <c>keccak256</c> digest — a kind of hash. Its digest gathers the bytes and hashes them once at the end:
/// the incremental keccak this app can reach (BouncyCastle's) is present twice, ambiguously, so keccak256 holds what it
/// is given until its hash is read.</summary>
public sealed class @this : kind.@this
{
    public @this() : base("keccak256") { }

    public override digest.@this Digest()
    {
        var gathered = new System.IO.MemoryStream();
        return new digest.@this(this, gathered.Write, () =>
        {
            using (gathered) return new Nethereum.Util.Sha3Keccack().CalculateHash(gathered.ToArray());
        });
    }
}
