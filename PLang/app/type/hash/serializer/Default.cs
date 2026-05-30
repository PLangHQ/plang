namespace app.type.hash.serializer;

/// <summary>
/// Wire renderer for <see cref="app.type.hash.@this"/> — the base64 digest.
/// Covers every format uniformly (the digest is opaque bytes; base64 is the
/// portable string form, and round-trips with <c>crypto.verify</c>). The
/// algorithm rides as the value's <c>kind</c> on the <c>type</c> envelope, not
/// in the value slot.
/// </summary>
public static class Default
{
    public static void Write(global::app.type.hash.@this value, global::app.channel.serializer.IWriter writer)
    {
        if (value == null) { writer.Null(); return; }
        writer.String(value.ToBase64());
    }
}
