namespace app.type.number.kind.biginteger;

/// <summary>The <c>biginteger</c> storage kind — the unbounded integer (the Ladder's top). CLR's
/// generic converter can't reach BigInteger, so it owns its arms (throws precise); a numeric value
/// lowers through its own exact-integer door, a string parses, anything else declines loud.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public override string Name => "biginteger";

    public override global::app.type.number.@this Create(global::app.type.item.@this value)
        => value.Clr<object>() switch
        {
            System.Numerics.BigInteger b => b,
            string s => System.Numerics.BigInteger.Parse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture),
            _ when value is global::app.type.number.@this n => n.AsBigInteger(),
            var o => throw new System.FormatException($"'{o}' cannot be biginteger."),
        };

    public override void Write(global::app.type.number.@this v, global::app.channel.serializer.IWriter w) => w.String(v.ToString());
    public override global::app.type.item.@this Read<TReader>(ref TReader r)
        => (global::app.type.number.@this)System.Numerics.BigInteger.Parse(r.String(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture);
}
