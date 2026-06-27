namespace app.type.image.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.image.@this"/> — the exact inverse of
/// <see cref="Default.Write"/> (the lossless byte form: base64 in JSON, raw bytes
/// in protobuf). <see cref="app.channel.serializer.IReader.Bytes"/> is
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
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.@null.@this("image", kind);
        byte[] bytes = reader.Bytes();
        string mime = ctx.Context?.App.Format.Mime("." + (kind ?? "")) ?? $"image/{kind}";
        return new global::app.type.image.@this(bytes, mime);
    }
}
