namespace app.type.guid.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.guid.@this"/> — the type reads its own value off the
/// single decode pass. Pulls the wire string form and re-houses the same
/// <c>guid.Convert</c> hook as <see cref="Default.Read"/> (canonical / braced /
/// hyphenless guid text all parse via <c>guid.Resolve</c>).
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.@null.@this("guid", kind);
        var raw = reader.String();
        return global::app.type.guid.@this.Convert(raw, kind, ctx.Context!).Peek()
            as global::app.type.item.@this
            ?? new global::app.type.@null.@this("guid", kind);
    }
}
