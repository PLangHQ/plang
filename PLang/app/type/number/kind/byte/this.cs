namespace app.type.number.kind.@byte;

/// <summary>The <c>byte</c> storage kind — 8-bit unsigned integer. ChangeType build; Int token.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public @this(global::app.actor.context.@this? context) : base("byte", context) { }
    public override System.Type? ClrForm => typeof(byte);
    public override void Write(global::app.type.number.@this value, global::app.channel.serializer.IWriter writer)
        => writer.Int(value.ToInt32());
    public override global::app.type.item.@this Read<TReader>(ref TReader reader)
        => global::app.type.number.@this.From((byte)reader.Int());
}
