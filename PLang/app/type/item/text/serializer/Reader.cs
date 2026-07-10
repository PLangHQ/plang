namespace app.type.item.text.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.item.text.@this"/> — the type reads its own value off the single
/// decode pass. Borns the text directly from the string token, the same shape as
/// <see cref="Default.Read"/>, born with the reader's template mode
/// (<c>ctx.Template</c>): an authored goal read carries <c>"plang"</c> and a
/// <c>%ref%</c> leaf borns a live template; a runtime-ingest read carries null and
/// the same bytes print literally. Text owns the holes-decision
/// (<see cref="app.type.item.text.@this.HasVariable"/>); resolution stays lazy at the door.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
        => reader.Null()
            ? new global::app.type.item.@null.@this("text", kind)
            : new global::app.type.item.text.@this(reader.String(), ctx.Template) { Kind = kind };
}
