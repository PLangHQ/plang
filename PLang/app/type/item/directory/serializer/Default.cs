namespace app.type.item.directory.serializer;

/// <summary>
/// Wire renderer for <see cref="@this"/> — a directory write-out is a FLAT
/// LISTING of its children's locations, never their contents (the listing
/// holds <c>path</c> values; each emits its location string). A recursive
/// tree is an explicit walk, not a serialization default. The listing is
/// lazy — an unlisted directory renders its location (the reference face).
/// </summary>
public static class Default
{
    public static void Write(global::app.type.item.directory.@this value, global::app.channel.serializer.IWriter writer)
    {
        if (value == null) { writer.Null(); return; }
        var listed = value.Listed;
        if (listed == null) { writer.String(value.ToString()); return; }
        var items = listed.Items;
        writer.BeginArray(items.Count);
        foreach (var entry in items)
            if (entry.Peek() is global::app.type.item.path.@this p) p.Write(writer);
            else writer.String(entry.Peek()?.ToString() ?? "");
        writer.EndArray();
    }
}
