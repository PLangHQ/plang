namespace app.type.item.number.kind.@byte;

/// <summary>The <c>byte</c> storage kind — 8-bit unsigned integer.</summary>
public sealed class @this : global::app.type.item.number.kind.@this
{
    public override string Name => "byte";
    public override global::app.type.item.number.@this Create(global::app.type.item.@this value) => value.Clr<byte>();
    public override void Write(global::app.type.item.number.@this v, global::app.channel.serializer.IWriter w) => w.Int(v.ToInt32());
    public override global::app.type.item.@this Read<TReader>(ref TReader r) => (global::app.type.item.number.@this)(byte)r.Int();
}
