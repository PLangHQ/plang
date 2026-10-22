namespace app.type.item.number.serializer;

using num = global::app.type.item.number.@this;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <see cref="app.type.item.number.@this"/>
/// — the inverse of <see cref="Default.Write"/>. The declared kind always rides the wire for a number,
/// so the reader hands the declared kind's own <c>Read</c> the token — the 15-arm switch dissolved onto
/// the kind classes (<c>type/number/kind/&lt;k&gt;</c>). What it reads is born holding the app's
/// <c>{number, &lt;its kind&gt;}</c>.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var types = ctx.Context.App.type.list;
        if (reader.Null()) return new global::app.type.item.@null.@this(types[new global::app.type.@this("number", kind), ctx.Context]);
        // A declared kind reads through the kind; a bare token with no declared kind (a literal a .pr row
        // holds as `number`) reads at its natural precision — the format reads its token by number's one rule
        // (an integer long, or biginteger past it; any other a double); a string parses through the family.
        var read = kind is not null && types[typeof(num)].kind[kind] is global::app.type.item.number.kind.@this k ? k.Read(ref reader)
            : reader.Peek() == global::app.type.format.TokenKind.String
                ? num.Create(new global::app.type.item.text.@this(reader.String(), types[typeof(global::app.type.item.text.@this)]))
                : num.Create(reader.Number());
        return read is num number
            ? new num(types[new global::app.type.@this("number", number.Kind.Name), ctx.Context], number)
            : read ?? new global::app.type.item.@null.@this(types[new global::app.type.@this("number", kind), ctx.Context]);
    }
}
