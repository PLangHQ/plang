namespace app.type.item.number.kind.@ulong;

/// <summary>The <c>ulong</c> storage kind: a 64-bit unsigned integer.</summary>
public sealed class @this : global::app.type.item.number.kind.@this
{
    public @this(global::app.type.@this owner) : base("ulong", owner) { }
    public override global::app.type.item.number.@this Create(global::app.type.item.@this value) => value.Clr<ulong>();

    public override global::app.type.item.@this Read<TReader>(ref TReader r)
        => (global::app.type.item.number.@this)(r.Peek() == global::app.type.format.TokenKind.String
            ? ulong.Parse(r.String(), System.Globalization.CultureInfo.InvariantCulture)
            : (ulong)r.Long());
}
