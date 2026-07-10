namespace app.type.item.number.kind.@long;

/// <summary>The <c>long</c> storage kind — 64-bit signed integer (the number default).</summary>
public sealed class @this : global::app.type.item.number.kind.@this
{
    public override string Name => "long";
    public override global::app.type.item.number.@this Create(global::app.type.item.@this value) => value.Clr<long>();
    public override void Write(global::app.type.item.number.@this v, global::app.channel.serializer.IWriter w) => w.Long(v.ToInt64());
    public override global::app.type.item.@this Read<TReader>(ref TReader r) => (global::app.type.item.number.@this)r.Long();
}
