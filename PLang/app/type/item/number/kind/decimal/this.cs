namespace app.type.item.number.kind.@decimal;

/// <summary>The <c>decimal</c> storage kind — 128-bit base-10 float.</summary>
public sealed class @this : global::app.type.item.number.kind.@this
{
    public override string Name => "decimal";
    public override global::app.type.item.number.@this Create(global::app.type.item.@this value) => value.Clr<decimal>();
    public override void Write(global::app.type.item.number.@this v, global::app.channel.serializer.IWriter w) => w.Decimal(v.ToDecimal());
    public override global::app.type.item.@this Read<TReader>(ref TReader r) => (global::app.type.item.number.@this)r.Decimal();
}
