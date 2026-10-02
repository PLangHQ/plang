namespace app.type.item.size.serializer;

/// <summary>
/// Read side for <c>size</c> in the reader registry — the "type reads itself" path: a count or a text in its
/// standard (<c>100 MB</c>, <c>95.4 MiB</c>) reads through <c>size.Create</c>.
/// </summary>
public static class Default
{
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
    {
        var carrier = new global::app.data.@this("", new global::app.type.item.@null.@this("size", kind), context: ctx.Context);
        return global::app.type.item.size.@this.Create(raw, carrier.Type, carrier);
    }
}
