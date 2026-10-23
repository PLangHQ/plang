namespace app.type.item.image.serializer;

/// <summary>
/// Read side for <see cref="app.type.item.image.@this"/>; an image writes itself.
/// </summary>
public static class Default
{
    /// <summary>
    /// A raw <c>byte[]</c> (the form a file
    /// or http channel hands an <c>image/&lt;kind&gt;</c> payload) becomes an
    /// image directly — image owns its byte materialization, the leaf serializer's
    /// job. A string raw (base64 / data-uri / path) routes through the per-family
    /// <c>image.Convert</c> hook unchanged.
    /// </summary>
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
    {
        var type = ctx.Context.App.type.list[new global::app.type.@this("image", kind), ctx.Context];
        // bytes the wire names no kind for are the kind their magic bytes name
        if (raw is byte[] bytes)
            return (kind == null ? global::app.type.item.image.@this.FromBytes(bytes, ctx.Context) : null)
                   ?? new global::app.type.item.image.@this(bytes, type);
        var carrier = new global::app.data.@this("", new global::app.type.item.@null.@this(type), context: ctx.Context);
        return global::app.type.item.image.@this.Create(raw, carrier.Type, carrier);
    }
}
