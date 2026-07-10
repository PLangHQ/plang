namespace app.type.item.number.kind.@ulong;

/// <summary>The <c>ulong</c> storage kind — 64-bit unsigned integer. Writes as a Long when it fits
/// <c>long.MaxValue</c>, else as its lossless invariant string.</summary>
public sealed class @this : global::app.type.item.number.kind.@this
{
    public override string Name => "ulong";
    public override global::app.type.item.number.@this Create(global::app.type.item.@this value) => value.Clr<ulong>();

    public override void Write(global::app.type.item.number.@this v, global::app.channel.serializer.IWriter w)
    {
        ulong ul = (ulong)v.BoxedValue;
        if (ul <= long.MaxValue) w.Long((long)ul);
        else w.String(ul.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public override global::app.type.item.@this Read<TReader>(ref TReader r)
        => (global::app.type.item.number.@this)(r.Peek() == global::app.channel.serializer.TokenKind.String
            ? ulong.Parse(r.String(), System.Globalization.CultureInfo.InvariantCulture)
            : (ulong)r.Long());
}
