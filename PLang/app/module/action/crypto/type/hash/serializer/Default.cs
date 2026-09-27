namespace app.module.action.crypto.type.hash.serializer;

/// <summary>
/// The ONE wire read-back for <see cref="app.module.action.crypto.type.hash.@this"/>: a base64 digest string
/// rebuilds the value; the algorithm rides as the kind on the type (falls back to keccak256, the signing
/// default). Discovered by the reader registry via the <c>&lt;typeName&gt;.serializer</c> namespace convention.
/// A hash writes itself (its base64 digest).
/// </summary>
public static class Default
{
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
        => raw is string s && !string.IsNullOrEmpty(s)
            ? global::app.module.action.crypto.type.hash.@this.FromBase64(s, kind ?? "keccak256")
            : null;
}
