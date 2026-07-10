namespace app.type.number.kind.@ushort;

/// <summary>The <c>ushort</c> storage kind — 16-bit unsigned integer. ChangeType build; Int token.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public @this(global::app.actor.context.@this? context) : base("ushort", context) { }
    public override System.Type? ClrForm => typeof(ushort);
    public override void Write(global::app.type.number.@this value, global::app.channel.serializer.IWriter writer)
        => writer.Int(value.ToInt32());
    public override global::app.type.item.@this Read<TReader>(ref TReader reader)
        => global::app.type.number.@this.From((ushort)reader.Int());
}
