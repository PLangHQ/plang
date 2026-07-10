namespace app.type.number.kind.uint128;

/// <summary>The <c>uint128</c> storage kind — 128-bit unsigned integer. Beyond ChangeType and the
/// writer's numeric vocabulary; builds from a string parse or the value's BigInteger, rides the wire
/// as its lossless invariant string.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public @this(global::app.actor.context.@this? context) : base("uint128", context) { }
    public override System.Type? ClrForm => typeof(System.UInt128);

    public override object Build(object value)
        => value is string s
            ? System.UInt128.Parse(s, System.Globalization.CultureInfo.InvariantCulture)
            : (System.UInt128)global::app.type.number.@this.FromObject(value)!.AsBigInteger();

    public override void Write(global::app.type.number.@this value, global::app.channel.serializer.IWriter writer)
        => writer.String(value.ToString());

    public override global::app.type.item.@this Read<TReader>(ref TReader reader)
        => global::app.type.number.@this.From(System.UInt128.Parse(reader.String(), System.Globalization.CultureInfo.InvariantCulture));
}
