namespace app.type.item.image.serializer;

/// <summary>
/// Text-writer renderer for <see cref="app.type.item.image.@this"/>. An image
/// can't sensibly render as base64 in a human-readable text stream — emit
/// the source path when one is wired, else a bare label so the line stays
/// scannable.
/// </summary>
public static class text
{
    public static void Write(global::app.type.item.image.@this value, global::app.channel.serializer.IWriter writer)
    {
        if (value == null) { writer.Null(); return; }
        writer.String(value.Path != null ? value.Path.ToString() : $"[image: {value.Mime} {value.Bytes.Length}B]");
    }
}
