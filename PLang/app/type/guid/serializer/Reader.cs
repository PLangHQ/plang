namespace app.type.guid.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.guid.@this"/> — the type reads its own value off the
/// single decode pass. The reader hands back the guid token directly, so the
/// value borns from it with no re-parse: <c>new guid(reader.Guid())</c>.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
        => reader.Null()
            ? new global::app.type.@null.@this("guid", kind)
            : new global::app.type.guid.@this(reader.Guid());
}
