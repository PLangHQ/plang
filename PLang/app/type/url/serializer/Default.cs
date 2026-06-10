namespace app.type.url.serializer;

/// <summary>
/// Wire renderer for <see cref="@this"/> — a url write-out is its fetched
/// CONTENT (same bare-scalar contract as <c>file</c>), pre-materialised by the
/// serialize chokepoint's <c>Load()</c> pass. An unfetched url renders its
/// location — write-out alone is not consent to fetch.
/// </summary>
public static class Default
{
    public static void Write(global::app.type.url.@this value, global::app.channel.serializer.IWriter writer)
    {
        if (value == null) { writer.Null(); return; }
        if (!value.IsLoaded) { writer.String(value.ToString()); return; }
        writer.String(value.ContentText());
    }
}
