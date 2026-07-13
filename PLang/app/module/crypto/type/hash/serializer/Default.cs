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

    /// <summary>
    /// Read mirror of <see cref="Write"/> — the ONE wire read-back for <c>hash</c>. A base64
    /// digest string rebuilds the value; the algorithm rides as <paramref name="kind"/>
    /// (falls back to keccak256, the signing default). The signing-side STJ
    /// <c>HashDataConverter</c> (object-shaped <c>{type,value}</c>) stays where its semantics apply.
    /// </summary>
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
        => raw is string s && !string.IsNullOrEmpty(s)
            ? global::app.module.crypto.type.hash.@this.FromBase64(s, kind ?? "keccak256")
            : null;
}
