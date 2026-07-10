namespace app.type.number.kind.@double;

/// <summary>The <c>double</c> storage kind — 64-bit binary float. ChangeType build; Double token.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public @this(global::app.actor.context.@this? context) : base("double", context) { }
    public override System.Type? ClrForm => typeof(double);
    public override void Write(global::app.type.number.@this value, global::app.channel.serializer.IWriter writer)
        => writer.Double(value.ToDouble());
    public override global::app.type.item.@this Read<TReader>(ref TReader reader)
        => global::app.type.number.@this.From(reader.Double());
}
