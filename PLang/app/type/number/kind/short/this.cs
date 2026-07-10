namespace app.type.number.kind.@short;

/// <summary>The <c>short</c> storage kind — 16-bit signed integer. ChangeType build; Int token.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public @this(global::app.actor.context.@this? context) : base("short", context) { }
    public override System.Type? ClrForm => typeof(short);
    public override void Write(global::app.type.number.@this value, global::app.channel.serializer.IWriter writer)
        => writer.Int(value.ToInt32());
    public override global::app.type.item.@this Read<TReader>(ref TReader reader)
        => global::app.type.number.@this.From((short)reader.Int());
}
