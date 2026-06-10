namespace app.type.image.serializer;

/// <summary>
/// Catch-all renderer for <see cref="app.type.image.@this"/> — base64.
/// Covers <c>json</c> + <c>plang</c> and any writer that doesn't ship a
/// specific format file. The base64 is the lossless portable form.
/// </summary>
public static class Default
{
    public static void Write(global::app.type.image.@this value, global::app.channel.serializer.IWriter writer)
    {
        if (value == null) { writer.Null(); return; }
        writer.String(System.Convert.ToBase64String(value.Bytes));
    }

    /// <summary>
    /// Read mirror of <see cref="Write"/>. A raw <c>byte[]</c> (the form a file
    /// or http channel hands an <c>image/&lt;kind&gt;</c> payload) becomes an
    /// image directly — image owns its byte materialization, the leaf serializer's
    /// job. A string raw (base64 / data-uri / path) routes through the per-family
    /// <c>image.Convert</c> hook unchanged.
    /// </summary>
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
    {
        if (raw is byte[] bytes)
        {
            var mime = ctx.Context?.App.Format.Mime("." + (kind ?? "")) ?? $"image/{kind}";
            return new global::app.type.image.@this(bytes, mime);
        }
        return global::app.type.image.@this.Convert(raw, kind, ctx.Context!)?.Peek();
    }
}
