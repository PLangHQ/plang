namespace app.type.item.duration.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.item.duration.@this"/> — the type reads its own value off the
/// single decode pass. A duration is written as text in either of its forms — the wire's
/// <c>ToString("c")</c> (<c>00:05:00</c>) or ISO 8601 as the type teaches it (<c>PT5M</c>),
/// the form a programmer or the LLM writes in a step's formal — and the type's own parse
/// (<see cref="app.type.item.duration.@this.Resolve"/>) reads both.
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
        return global::app.type.item.duration.@this.Resolve(text, ctx.Context)
            ?? throw new System.FormatException(
                $"'{text}' is not a duration — write it in ISO 8601 (PT5M) or as 00:05:00.");
    }
}
