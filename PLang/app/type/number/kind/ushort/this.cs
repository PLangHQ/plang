namespace app.type.number.kind.@ushort;

/// <summary>The <c>ushort</c> storage kind — 16-bit unsigned integer.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public override string Name => "ushort";
    public override global::app.type.number.@this Create(global::app.type.item.@this value) => value.Clr<ushort>();
    public override void Write(global::app.type.number.@this v, global::app.channel.serializer.IWriter w) => w.Int(v.ToInt32());
    public override global::app.type.item.@this Read<TReader>(ref TReader r) => (global::app.type.number.@this)(ushort)r.Int();
}
