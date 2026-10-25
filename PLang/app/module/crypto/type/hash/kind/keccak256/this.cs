namespace app.module.crypto.type.hash.kind.keccak256;

/// <summary>The <c>keccak256</c> digest — a kind of hash: BouncyCastle's Keccak at 256 bits (the original Keccak
/// padding, not FIPS SHA-3), taking the bytes as they come.</summary>
public sealed class @this : kind.@this
{
    public @this(global::app.type.@this owner) : base("keccak256", owner) { }

    public override digest.@this Digest()
    {
        var keccak = new Org.BouncyCastle.Crypto.Digests.KeccakDigest(256);
        return new digest.@this(this, keccak.BlockUpdate, () =>
        {
            var hash = new byte[keccak.GetDigestSize()];
            keccak.DoFinal(hash, 0);
            return hash;
        });
    }
}
