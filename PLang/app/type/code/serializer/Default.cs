namespace app.type.code.serializer;

/// <summary>
/// Read side for <see cref="app.type.code.@this"/> — source text decodes to a <c>code</c> value, the kind
/// naming the language (html/css/js). Content off I/O rides as binary bytes; the source is text, so it
/// decodes through the text type (which owns bytes→string). A code value writes itself.
/// </summary>
public static class Default
{
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
    {
        if (raw is not (string or byte[])) return raw;
        string source = new global::app.type.item.text.@this(raw).ToString();
        return new global::app.type.code.@this(source, kind ?? "text");
    }
}
