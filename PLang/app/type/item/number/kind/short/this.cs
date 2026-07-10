namespace app.type.item.number.kind.@short;

/// <summary>The <c>short</c> storage kind — 16-bit signed integer.</summary>
public sealed class @this : global::app.type.item.number.kind.@this
{
    public override string Name => "short";
    public override global::app.type.item.number.@this Create(global::app.type.item.@this value) => value.Clr<short>();
    public override void Write(global::app.type.item.number.@this v, global::app.channel.serializer.IWriter w) => w.Int(v.ToInt32());
    public override global::app.type.item.@this Read<TReader>(ref TReader r) => (global::app.type.item.number.@this)(short)r.Int();
}
