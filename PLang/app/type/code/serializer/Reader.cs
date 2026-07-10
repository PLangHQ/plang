namespace app.type.code.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.code.@this"/> — the inverse of <see cref="Default.Write"/>
/// (<c>writer.String(Source)</c>). Source text pulls off the single decode pass as
/// a string token and borns the <c>code</c> value, the <paramref name="kind"/>
/// naming the language (html/css/js, default text). Content off I/O rides as binary
/// bytes through the <c>binary</c> family, never here.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
        => reader.Null()
            ? new global::app.type.item.@null.@this("code", kind)
            : new global::app.type.code.@this(reader.String(), kind ?? "text");
}
