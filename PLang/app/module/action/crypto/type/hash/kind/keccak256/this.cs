namespace app.module.action.crypto.type.hash.kind.keccak256;

/// <summary>The <c>keccak256</c> digest — a kind of hash.</summary>
public sealed class @this : global::app.type.kind.@this
{
    public @this(global::app.actor.context.@this? context = null) : base("keccak256", context) { }

    protected internal override string Owner => "hash";
}
