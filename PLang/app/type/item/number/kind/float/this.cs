namespace app.type.item.number.kind.@float;

/// <summary>The <c>float</c> storage kind — 32-bit binary float.</summary>
public sealed class @this : global::app.type.item.number.kind.@this
{
    public override string Name => "float";
    public override global::app.type.item.number.@this Create(global::app.type.item.@this value) => value.Clr<float>();
    public override void Write(global::app.type.item.number.@this v, global::app.channel.serializer.IWriter w) => w.Float(v.ToSingle());
    public override global::app.type.item.@this Read<TReader>(ref TReader r) => (global::app.type.item.number.@this)r.Float();
}
