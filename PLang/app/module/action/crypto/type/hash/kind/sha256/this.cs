namespace app.module.action.crypto.type.hash.kind.sha256;

/// <summary>The <c>sha256</c> digest — a kind of hash.</summary>
public sealed class @this : global::app.type.kind.@this
{
    public @this(global::app.actor.context.@this? context = null) : base("sha256", context) { }

    protected internal override string Owner => "hash";
}
