namespace app.type.item.number.kind.@uint;

/// <summary>The <c>uint</c> storage kind — 32-bit unsigned integer.</summary>
public sealed class @this : global::app.type.item.number.kind.@this
{
    public @this(global::app.type.@this owner) : base("uint", owner) { }
    public override global::app.type.item.number.@this Create(global::app.type.item.@this value) => value.Clr<uint>();
    public override global::app.type.item.@this Read<TReader>(ref TReader r) => (global::app.type.item.number.@this)(uint)r.Long();
}
