namespace app.module.crypto.type.hash.kind;

/// <summary>
/// A kind of hash — an algorithm (<c>sha256</c>, <c>keccak256</c>). Each takes its digest itself, incrementally
/// (<see cref="Digest()"/>), so a hash is made from bytes as they come — a file or a download is never held whole —
/// with no switch over algorithm names.
/// </summary>
public abstract class @this : global::app.type.kind.@this
{
    protected @this(string name) : base(name) { }

    protected internal override string Owner => "hash";

    /// <summary>A digest begun: bytes are added to it as they come, then its hash is read.</summary>
    public abstract digest.@this Digest();

    /// <summary>The digest of <paramref name="bytes"/>, held whole.</summary>
    internal byte[] Digest(byte[] bytes) => Digest().Add(bytes).Hash.Bytes;
}
