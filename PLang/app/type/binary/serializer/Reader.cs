namespace app.type.binary.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>binary</c> —
/// raw bytes with no kind that names a richer type (octet-stream / unset mime). The
/// value IS its bytes; the reader takes them off the pass and borns the binary value.
/// A byte form whose kind names an inner type (json→item, jpg→image, csv→table) is
/// narrowed to that type before reaching here (the reader registry's narrowing).
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.@null.@this("binary", kind);
        return new global::app.type.binary.@this(reader.Bytes()) { Kind = kind };
    }
}
