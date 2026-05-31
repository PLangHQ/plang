namespace app.module.crypto.type.hash.serializer;

/// <summary>
/// Wire renderer for <see cref="app.module.crypto.type.hash.@this"/> — the
/// base64 digest. Covers every format uniformly (the digest is opaque bytes;
/// base64 is the portable string form, and round-trips with <c>crypto.verify</c>).
/// The algorithm rides as the value's <c>kind</c> on the <c>type</c> envelope,
/// not in the value slot. Discovered by the per-(type, format) renderer registry
/// via the <c>&lt;typeName&gt;.serializer</c> namespace convention, so the
/// move out of <c>app/type/</c> keeps it wired.
/// </summary>
public static class Default
{
    public static void Write(global::app.module.crypto.type.hash.@this value, global::app.channel.serializer.IWriter writer)
    {
        if (value == null) { writer.Null(); return; }
        writer.String(value.ToBase64());
    }
}
