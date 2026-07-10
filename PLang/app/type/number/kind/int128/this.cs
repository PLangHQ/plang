namespace app.type.number.kind.int128;

/// <summary>The <c>int128</c> storage kind — 128-bit signed integer. Beyond ChangeType and the
/// writer's numeric vocabulary, so it builds from a string parse or the value's BigInteger and
/// rides the wire as its lossless invariant string.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public @this(global::app.actor.context.@this? context) : base("int128", context) { }
    public override System.Type? ClrForm => typeof(System.Int128);

    public override object Build(object value)
        => value is string s
            ? System.Int128.Parse(s, System.Globalization.CultureInfo.InvariantCulture)
            : (System.Int128)global::app.type.number.@this.FromObject(value)!.AsBigInteger();

    public override void Write(global::app.type.number.@this value, global::app.channel.serializer.IWriter writer)
        => writer.String(value.ToString());

    public override global::app.type.item.@this Read<TReader>(ref TReader reader)
        => global::app.type.number.@this.From(System.Int128.Parse(reader.String(), System.Globalization.CultureInfo.InvariantCulture));
}
