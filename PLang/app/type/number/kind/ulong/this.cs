namespace app.type.number.kind.@ulong;

/// <summary>The <c>ulong</c> storage kind — 64-bit unsigned integer. ChangeType build; writes as a
/// Long when it fits <c>long.MaxValue</c>, else as its lossless invariant string.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public @this(global::app.actor.context.@this? context) : base("ulong", context) { }
    public override System.Type? ClrForm => typeof(ulong);

    public override void Write(global::app.type.number.@this value, global::app.channel.serializer.IWriter writer)
    {
        ulong ul = (ulong)value.BoxedValue;
        if (ul <= long.MaxValue) writer.Long((long)ul);
        else writer.String(ul.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public override global::app.type.item.@this Read<TReader>(ref TReader reader)
        => global::app.type.number.@this.From(reader.Peek() == global::app.channel.serializer.TokenKind.String
            ? ulong.Parse(reader.String(), System.Globalization.CultureInfo.InvariantCulture)
            : (ulong)reader.Long());
}
