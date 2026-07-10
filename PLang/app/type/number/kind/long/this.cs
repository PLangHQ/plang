namespace app.type.number.kind.@long;

/// <summary>The <c>long</c> storage kind — 64-bit signed integer (the number default). ChangeType
/// build; Long token.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public @this(global::app.actor.context.@this? context) : base("long", context) { }
    public override System.Type? ClrForm => typeof(long);
    public override void Write(global::app.type.number.@this value, global::app.channel.serializer.IWriter writer)
        => writer.Long(value.ToInt64());
    public override global::app.type.item.@this Read<TReader>(ref TReader reader)
        => global::app.type.number.@this.From(reader.Long());
}
