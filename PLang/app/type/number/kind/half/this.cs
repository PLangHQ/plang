namespace app.type.number.kind.half;

/// <summary>The <c>half</c> storage kind — 16-bit binary float. ChangeType can't reach Half, so it
/// builds from a string parse or the value's double; writes/reads through the Double token.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public @this(global::app.actor.context.@this? context) : base("half", context) { }
    public override System.Type? ClrForm => typeof(System.Half);

    public override object Build(object value)
        => value is string s
            ? System.Half.Parse(s, System.Globalization.CultureInfo.InvariantCulture)
            : (System.Half)global::app.type.number.@this.FromObject(value)!.AsDouble();

    public override global::app.type.number.@this FromDouble(double m) => global::app.type.number.@this.From((System.Half)m);

    public override void Write(global::app.type.number.@this value, global::app.channel.serializer.IWriter writer)
        => writer.Double(value.ToDouble());

    public override global::app.type.item.@this Read<TReader>(ref TReader reader)
        => global::app.type.number.@this.From((System.Half)reader.Double());
}
