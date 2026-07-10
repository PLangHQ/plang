namespace app.type.number.kind.int128;

/// <summary>The <c>int128</c> storage kind — 128-bit signed integer. CLR's generic converter can't
/// reach Int128, so it owns its arms (throws precise); rides the wire as its invariant string.</summary>
public sealed class @this : global::app.type.number.kind.@this
{
    public override string Name => "int128";

    public override global::app.type.number.@this Create(global::app.type.item.@this value)
        => value.Clr<object>() switch
        {
            System.Int128 v => v,
            string s => System.Int128.Parse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture),
            _ when value is global::app.type.number.@this n => (System.Int128)n.AsBigInteger(),
            var o => throw new System.FormatException($"'{o}' cannot be int128."),
        };

    public override void Write(global::app.type.number.@this v, global::app.channel.serializer.IWriter w) => w.String(v.ToString());
    public override global::app.type.item.@this Read<TReader>(ref TReader r)
        => (global::app.type.number.@this)System.Int128.Parse(r.String(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture);
}
