namespace app.type.item.number.kind.@sbyte;

/// <summary>The <c>sbyte</c> storage kind — 8-bit signed integer.</summary>
public sealed class @this : global::app.type.item.number.kind.@this
{
    public @this(global::app.type.@this owner) : base("sbyte", owner) { }
    public override global::app.type.item.number.@this Create(global::app.type.item.@this value) => value.Clr<sbyte>();
    public override global::app.type.item.@this Read<TReader>(ref TReader r) => (global::app.type.item.number.@this)(sbyte)r.Int();
}
