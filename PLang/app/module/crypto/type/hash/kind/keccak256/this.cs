namespace app.module.crypto.type.hash.kind.keccak256;

/// <summary>The <c>keccak256</c> digest — a kind of hash.</summary>
public sealed class @this : kind.@this
{
    public @this() : base("keccak256") { }

    internal override byte[] Digest(byte[] bytes) => new Nethereum.Util.Sha3Keccack().CalculateHash(bytes);
}
