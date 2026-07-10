namespace app.type.number.kind.@int;

/// <summary>The <c>int</c> storage kind — a 32-bit signed integer. Builds by the base
/// <c>ChangeType</c>; writes/reads the native Int token.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public @this(global::app.actor.context.@this? context) : base("int", context) { }

    public override System.Type? ClrForm => typeof(int);

    public override void Write(global::app.type.number.@this value, global::app.channel.serializer.IWriter writer)
        => writer.Int(value.ToInt32());

    public override global::app.type.item.@this Read<TReader>(ref TReader reader)
        => global::app.type.number.@this.From(reader.Int());
}
