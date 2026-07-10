namespace app.type.number.kind.@float;

/// <summary>The <c>float</c> storage kind — 32-bit binary float. ChangeType build; Float token;
/// build-from-double narrows to float.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public @this(global::app.actor.context.@this? context) : base("float", context) { }
    public override System.Type? ClrForm => typeof(float);
    public override global::app.type.number.@this FromDouble(double m) => global::app.type.number.@this.From((float)m);
    public override void Write(global::app.type.number.@this value, global::app.channel.serializer.IWriter writer)
        => writer.Float(value.ToSingle());
    public override global::app.type.item.@this Read<TReader>(ref TReader reader)
        => global::app.type.number.@this.From(reader.Float());
}
