namespace app.type.item.file.serializer;

/// <summary>
/// Wire renderer for <see cref="@this"/> — a file write-out is its CONTENT
/// (the bare-scalar contract: <c>write out %file%</c> emits what was read,
/// never the location string). The content was pre-materialised by the
/// serialize chokepoint's <c>Load()</c> pass (<c>file</c> is
/// <c>ILoadable</c>), so the sync renderer reads the in-memory bytes. Text
/// mime emits the UTF-8 text form; anything else emits the bytes.
/// </summary>
public static class Default
{
    public static void Write(global::app.type.item.file.@this value, global::app.channel.serializer.IWriter writer)
    {
        if (value == null) { writer.Null(); return; }
        var mime = value.Path.MimeType;
        if (mime.StartsWith("text/", System.StringComparison.OrdinalIgnoreCase)
            || mime.Contains("json", System.StringComparison.OrdinalIgnoreCase)
            || mime.Contains("xml", System.StringComparison.OrdinalIgnoreCase))
            writer.String(value.ContentText());
        else
            writer.Bytes(value.Bytes);
    }
}
