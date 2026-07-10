namespace app.type.item.number.kind.half;

/// <summary>The <c>half</c> storage kind — 16-bit binary float. Its only C# road in is via double.</summary>
public sealed class @this : global::app.type.item.number.kind.@this
{
    public override string Name => "half";
    public override global::app.type.item.number.@this Create(global::app.type.item.@this value) => (System.Half)value.Clr<double>();
    public override void Write(global::app.type.item.number.@this v, global::app.channel.serializer.IWriter w) => w.Double(v.ToDouble());
    public override global::app.type.item.@this Read<TReader>(ref TReader r) => (global::app.type.item.number.@this)(System.Half)r.Double();
}
