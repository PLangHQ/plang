namespace app.type.number.kind.@decimal;

/// <summary>The <c>decimal</c> storage kind — 128-bit base-10 float. ChangeType build; Decimal token.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public @this(global::app.actor.context.@this? context) : base("decimal", context) { }
    public override System.Type? ClrForm => typeof(decimal);
    public override void Write(global::app.type.number.@this value, global::app.channel.serializer.IWriter writer)
        => writer.Decimal(value.ToDecimal());
    public override global::app.type.item.@this Read<TReader>(ref TReader reader)
        => global::app.type.number.@this.From(reader.Decimal());
}
