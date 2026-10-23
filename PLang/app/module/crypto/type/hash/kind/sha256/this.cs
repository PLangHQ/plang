namespace app.module.crypto.type.hash.kind.sha256;

/// <summary>The <c>sha256</c> digest — a kind of hash.</summary>
public sealed class @this : kind.@this
{
    public @this(global::app.type.@this owner) : base("sha256", owner) { }

    public override digest.@this Digest()
    {
        var sha = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        return new digest.@this(this, sha.AppendData, () =>
        {
            using (sha) return sha.GetHashAndReset();
        });
    }
}
