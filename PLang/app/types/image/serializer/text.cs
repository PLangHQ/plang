namespace app.types.image.serializer;

/// <summary>
/// Text-writer renderer for <see cref="app.types.image.@this"/>. An image
/// can't sensibly render as base64 in a human-readable text stream — emit
/// the source path when one is wired, else a bare label so the line stays
/// scannable.
/// </summary>
public static class text
{
    public static void Write(global::app.types.image.@this value, global::app.channels.serializers.IWriter writer)
    {
        if (value == null) { writer.Null(); return; }
        if (value.Path != null)
        {
            try { writer.String(value.Path.Relative); return; }
            catch (System.Exception ex) when (ex is not (System.OutOfMemoryException or System.StackOverflowException))
            { /* fall through to bare label */ }
        }
        writer.String($"[image: {value.Mime} {value.Bytes.Length}B]");
    }
}
