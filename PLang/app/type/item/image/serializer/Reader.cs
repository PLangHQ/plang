namespace app.type.item.image.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.item.image.@this"/> — the exact inverse of
/// <see cref="Default.Write"/> (the lossless byte form: base64 in JSON, raw bytes
/// in protobuf). <see cref="app.type.format.IReader.Bytes"/> is
/// format-agnostic — JSON decodes the base64 token, a bytes reader hands the blob
/// through — so the image borns from its own bytes, the kind naming the mime. A
/// path-string image is the lazy-handle CONTENT form (<c>image.Convert</c>), a
/// different concern off I/O — never the wire value.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var type = ctx.Context.App.type.list[new global::app.type.@this("image", kind), ctx.Context];
        if (reader.Null()) return new global::app.type.item.@null.@this(type);
        byte[] bytes = reader.Bytes();
        // bytes the wire names no kind for are the kind their magic bytes name
        return (kind == null ? global::app.type.item.image.@this.FromBytes(bytes, ctx.Context) : null)
               ?? new global::app.type.item.image.@this(bytes, type);
    }
}
