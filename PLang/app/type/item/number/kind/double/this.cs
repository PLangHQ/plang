namespace app.type.item.number.kind.@double;

/// <summary>The <c>double</c> storage kind — 64-bit binary float.</summary>
public sealed class @this : global::app.type.item.number.kind.@this
{
    public @this(global::app.type.@this owner) : base("double", owner) { }
    public override global::app.type.item.number.@this Create(global::app.type.item.@this value) => value.Clr<double>();
    public override global::app.type.item.@this Read<TReader>(ref TReader r) => (global::app.type.item.number.@this)r.Double();
}
