namespace app.type.code.serializer;

/// <summary>
/// Catch-all renderer for <see cref="app.type.code.@this"/> — the source
/// text. Uniform across <c>json</c>, <c>plang</c>, and <c>text</c>; an
/// HTML writer (which would wrap in <c>&lt;pre&gt;&lt;code&gt;</c>) is a
/// follow-up.
/// </summary>
public static class Default
{
    public static void Write(global::app.type.code.@this value, global::app.channel.serializer.IWriter writer)
    {
        if (value == null) { writer.Null(); return; }
        writer.String(value.Source);
    }

    /// <summary>
    /// Read side — source text decodes to a <c>code</c> value, the kind naming the
    /// language (html/css/js). Content off I/O rides as binary bytes; the source is
    /// text, so it decodes through the text type (which owns bytes→string).
    /// </summary>
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
    {
        if (raw is not (string or byte[])) return raw;
        string source = new global::app.type.item.text.@this(raw).ToString();
        return new global::app.type.code.@this(source, kind ?? "text");
    }
}
