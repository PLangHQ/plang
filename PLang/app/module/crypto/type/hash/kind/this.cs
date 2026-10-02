namespace app.module.crypto.type.hash.kind;

/// <summary>
/// A kind of hash — an algorithm (<c>sha256</c>, <c>keccak256</c>). Each digests bytes itself, so the hash is made
/// from bytes by its kind (<see cref="hash.@this.Of"/>), with no switch over algorithm names.
/// </summary>
public abstract class @this : global::app.type.kind.@this
{
    protected @this(string name) : base(name) { }

    protected internal override string Owner => "hash";

    /// <summary>The digest of <paramref name="bytes"/>.</summary>
    internal abstract byte[] Digest(byte[] bytes);
}
