namespace app.module.crypto.type.hash.kind.sha256;

/// <summary>The <c>sha256</c> digest — a kind of hash.</summary>
public sealed class @this : kind.@this
{
    public @this() : base("sha256") { }

    internal override byte[] Digest(byte[] bytes) => System.Security.Cryptography.SHA256.HashData(bytes);
}
