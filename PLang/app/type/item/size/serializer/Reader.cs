namespace app.type.item.size.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <see cref="app.type.item.size.@this"/> — the type
/// reads its own value off the single decode pass. A size is written as its text in its kind (<c>100 MB</c>,
/// <c>95.4 MiB</c>); the kind the wire names reads it, and a text with none is read by the kind whose suffix it has.
/// A bare number is a count of bytes.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("size", kind);
        var text = reader.Peek() == global::app.type.format.TokenKind.Number ? reader.Number().ToString() ?? "" : reader.String();
        var named = string.IsNullOrEmpty(kind) ? null
            : ctx.Context.App.type.list["size"].kind[kind!] as global::app.type.item.size.kind.@this;
        return (named?.Parse(text, ctx.Context) ?? global::app.type.item.size.@this.Resolve(text, ctx.Context))
            ?? throw new System.FormatException(
                $"'{text}' is not a size — write a number and its unit: IEC (500 KiB) or SI (100 MB).");
    }
}
