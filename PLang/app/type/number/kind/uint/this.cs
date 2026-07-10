namespace app.type.number.kind.@uint;

/// <summary>The <c>uint</c> storage kind — 32-bit unsigned integer. ChangeType build; Long token.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public @this(global::app.actor.context.@this? context) : base("uint", context) { }
    public override System.Type? ClrForm => typeof(uint);
    public override void Write(global::app.type.number.@this value, global::app.channel.serializer.IWriter writer)
        => writer.Long(value.ToInt64());
    public override global::app.type.item.@this Read<TReader>(ref TReader reader)
        => global::app.type.number.@this.From((uint)reader.Long());
}
