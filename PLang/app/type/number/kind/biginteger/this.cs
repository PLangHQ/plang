namespace app.type.number.kind.biginteger;

/// <summary>The <c>biginteger</c> storage kind — the unbounded integer (the Ladder's top). Builds
/// from a string parse or the value's BigInteger; rides the wire as its lossless invariant string.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public @this(global::app.actor.context.@this? context) : base("biginteger", context) { }
    public override System.Type? ClrForm => typeof(System.Numerics.BigInteger);

    public override object Build(object value)
        => value is string s
            ? System.Numerics.BigInteger.Parse(s, System.Globalization.CultureInfo.InvariantCulture)
            : global::app.type.number.@this.FromObject(value)!.AsBigInteger();

    public override void Write(global::app.type.number.@this value, global::app.channel.serializer.IWriter writer)
        => writer.String(value.ToString());

    public override global::app.type.item.@this Read<TReader>(ref TReader reader)
        => global::app.type.number.@this.From(System.Numerics.BigInteger.Parse(reader.String(), System.Globalization.CultureInfo.InvariantCulture));
}
