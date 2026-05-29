namespace app.types.image.serializer;

/// <summary>
/// Catch-all renderer for <see cref="app.types.image.@this"/> — base64.
/// Covers <c>json</c> + <c>plang</c> and any writer that doesn't ship a
/// specific format file. The base64 is the lossless portable form.
/// </summary>
public static class Default
{
    public static void Write(global::app.types.image.@this value, global::app.channel.serializer.IWriter writer)
    {
        if (value == null) { writer.Null(); return; }
        writer.String(System.Convert.ToBase64String(value.Bytes));
    }
}
