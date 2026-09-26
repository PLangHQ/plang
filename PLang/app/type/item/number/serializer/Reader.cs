namespace app.type.item.number.serializer;

using num = global::app.type.item.number.@this;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <see cref="app.type.item.number.@this"/>
/// — the inverse of <see cref="Default.Write"/>. The declared kind always rides the wire for a number,
/// so the reader hands the declared kind's own <c>Read</c> the token — the 15-arm switch dissolved onto
/// the kind classes (<c>type/number/kind/&lt;k&gt;</c>).
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("number", kind);
        // A declared kind reads through the kind; a bare token with no declared kind (a literal a .pr row
        // holds as `number`) reads at its natural precision — an integral token is an integer (long), any
        // other a double; a string parses through the family.
        if (kind is not null && num.Kinds.TryGetValue(kind, out var k))
            return k.Read(ref reader);
        if (reader.Peek() == global::app.channel.serializer.TokenKind.String)
            return num.Create(new global::app.type.item.text.@this(reader.String())) ?? (global::app.type.item.@this)
                new global::app.type.item.@null.@this("number", kind);
        return reader.Number() switch
        {
            long integral => (num)integral,
            var other => (num)System.Convert.ToDouble(other, System.Globalization.CultureInfo.InvariantCulture),
        };
    }
}
