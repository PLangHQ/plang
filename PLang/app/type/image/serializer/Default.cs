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
    /// Read mirror of <see cref="Write"/> — re-houses the per-family
    /// <c>image.Convert</c> hook (base64 / data-uri / path → image) behind the
    /// reader registry. The decode logic is not rewritten.
    /// </summary>
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
        => global::app.type.image.@this.Convert(raw, kind, ctx.Context!)?.Value;
}
