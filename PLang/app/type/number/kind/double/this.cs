namespace app.type.number.kind.@double;

/// <summary>The <c>double</c> storage kind — 64-bit binary float.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public override string Name => "double";
    public override global::app.type.number.@this Create(global::app.type.item.@this value) => value.Clr<double>();
    public override void Write(global::app.type.number.@this v, global::app.channel.serializer.IWriter w) => w.Double(v.ToDouble());
    public override global::app.type.item.@this Read<TReader>(ref TReader r) => (global::app.type.number.@this)r.Double();
}
