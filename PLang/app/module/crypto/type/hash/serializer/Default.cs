namespace app.module.crypto.type.hash.serializer;

/// <summary>
/// The ONE wire read-back for <see cref="app.module.crypto.type.hash.@this"/>: a base64 digest string
/// rebuilds the value; the algorithm rides as the kind on the type — none written, the digest has none (never a
/// guessed one), and a name that is no kind of hash refuses the read. Discovered by the reader registry via the
/// <c>&lt;typeName&gt;.serializer</c> namespace convention. A hash writes itself (its base64 digest).
/// </summary>
public static class Default
{
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
        => raw is string s && !string.IsNullOrEmpty(s)
            ? global::app.module.crypto.type.hash.@this.FromBase64(s,
                string.IsNullOrEmpty(kind) ? null
                    : global::app.type.item.choice.@this<global::app.module.crypto.type.hash.kind.@this>.Parse(kind).Value)
            : null;
}
