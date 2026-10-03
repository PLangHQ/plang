namespace app.type.item.separator.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <see cref="app.type.item.separator.@this"/> — the
/// mirror of its <c>Write</c>: a separator's name reads as that separator, any other text as its characters.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("separator", kind);
        var text = reader.String();
        return global::app.type.item.separator.@this.Create(text, null, new global::app.data.@this("", context: ctx.Context))!;
    }
}
