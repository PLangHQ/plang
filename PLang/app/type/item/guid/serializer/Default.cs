namespace app.type.item.guid.serializer;

/// <summary>
/// Read side for <c>guid</c> in the reader registry — the "type reads itself"
/// path. Re-houses the per-family <c>guid.Convert</c> hook (canonical / braced /
/// hyphenless guid text all parse via <c>guid.Resolve</c>).
/// </summary>
public static class Default
{
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
    {
        var carrier = new global::app.data.@this("", new global::app.type.item.@null.@this(ctx.Context.App.type.list[new global::app.type.@this("guid", kind), ctx.Context]), context: ctx.Context);
        return global::app.type.item.guid.@this.Create(raw, carrier.Type, carrier);
    }
}
