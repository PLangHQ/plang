namespace app.type.duration.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.duration.@this"/> — the type reads its own value off the
/// single decode pass. The reader hands back the timespan token directly (the
/// wire form the writer emits via <c>ToString("c")</c>), so the value borns from
/// it with no re-parse: <c>new duration(reader.TimeSpan())</c>. The ISO-8601
/// <c>PT30S</c> form is a runtime-ingest concern (content decode), not the wire read.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
        => reader.Null()
            ? new global::app.type.item.@null.@this("duration", kind)
            : new global::app.type.duration.@this(reader.TimeSpan());
}
