namespace app.type.item.image.serializer;

/// <summary>
/// Read side for <see cref="app.type.item.image.@this"/>; an image writes itself.
/// </summary>
public static class Default
{
    /// <summary>
    /// A raw <c>byte[]</c> (the form a file
    /// or http channel hands an <c>image/&lt;kind&gt;</c> payload) becomes an
    /// image directly — image owns its byte materialization, the leaf serializer's
    /// job. A string raw (base64 / data-uri / path) routes through the per-family
    /// <c>image.Convert</c> hook unchanged.
    /// </summary>
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
    {
        if (raw is byte[] bytes)
        {
            var mime = ctx.Context == null ? $"image/{kind}"
                : (string.IsNullOrEmpty(kind) ? null : ctx.Context.App.type.list["image"].kind[kind]?.Mime.FirstOrDefault()) ?? "application/octet-stream";
            return new global::app.type.item.image.@this(bytes, mime, kind);
        }
        return global::app.type.item.image.@this.Create(raw,
            new global::app.data.@this("", new global::app.type.item.@null.@this("image", kind), context: ctx.Context));
    }
}
