namespace app.type.number.kind.@uint;

/// <summary>The <c>uint</c> storage kind — 32-bit unsigned integer.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public override string Name => "uint";
    public override global::app.type.number.@this Create(global::app.type.item.@this value) => value.Clr<uint>();
    public override void Write(global::app.type.number.@this v, global::app.channel.serializer.IWriter w) => w.Long(v.ToInt64());
    public override global::app.type.item.@this Read<TReader>(ref TReader r) => (global::app.type.number.@this)(uint)r.Long();
}
