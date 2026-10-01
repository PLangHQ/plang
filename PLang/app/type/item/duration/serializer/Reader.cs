namespace app.type.item.duration.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.item.duration.@this"/> — the type reads its own value off the single decode pass. A
/// duration is written as its text in its kind (<c>30s</c>, <c>PT5M</c>, <c>00:05:00</c>); the kind the wire
/// names reads it, and a text with none (an older .pr) is read by the kind whose form it is.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("duration", kind);
        var text = reader.String();
        var named = string.IsNullOrEmpty(kind) ? null
            : ctx.Context.App.type.list["duration"].kind[kind!] as global::app.type.item.duration.kind.@this;
        return (named?.Parse(text, ctx.Context) ?? global::app.type.item.duration.@this.Resolve(text, ctx.Context))
            ?? throw new System.FormatException(
                $"'{text}' is not a duration — write a number and its unit (30s), ISO 8601 (PT30S) or 00:00:30.");
    }
}
