namespace app.type.number.kind.@sbyte;

/// <summary>The <c>sbyte</c> storage kind — 8-bit signed integer. ChangeType build; Int token.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public @this(global::app.actor.context.@this? context) : base("sbyte", context) { }
    public override System.Type? ClrForm => typeof(sbyte);
    public override void Write(global::app.type.number.@this value, global::app.channel.serializer.IWriter writer)
        => writer.Int(value.ToInt32());
    public override global::app.type.item.@this Read<TReader>(ref TReader reader)
        => global::app.type.number.@this.From((sbyte)reader.Int());
}
