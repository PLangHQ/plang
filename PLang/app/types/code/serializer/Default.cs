namespace app.types.code.serializer;

/// <summary>
/// Catch-all renderer for <see cref="app.types.code.@this"/> — the source
/// text. Uniform across <c>json</c>, <c>plang</c>, and <c>text</c>; an
/// HTML writer (which would wrap in <c>&lt;pre&gt;&lt;code&gt;</c>) is a
/// follow-up.
/// </summary>
public static class Default
{
    public static void Write(global::app.types.code.@this value, global::app.channel.serializer.IWriter writer)
    {
        if (value == null) { writer.Null(); return; }
        writer.String(value.Source);
    }
}
