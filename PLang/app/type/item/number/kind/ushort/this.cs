namespace app.type.item.number.kind.@ushort;

/// <summary>The <c>ushort</c> storage kind — 16-bit unsigned integer.</summary>
public sealed class @this : global::app.type.item.number.kind.@this
{
    public @this(global::app.type.@this owner) : base("ushort", owner) { }
    public override global::app.type.item.number.@this Create(global::app.type.item.@this value) => value.Clr<ushort>();
    public override global::app.type.item.@this Read<TReader>(ref TReader r) => (global::app.type.item.number.@this)(ushort)r.Int();
}
