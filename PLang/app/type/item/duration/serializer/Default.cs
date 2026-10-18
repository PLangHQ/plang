namespace app.type.item.duration.serializer;

/// <summary>
/// Read side for <c>duration</c> in the reader registry — the "type reads
/// itself" path. Re-houses the per-family <c>duration.Convert</c> hook
/// (ISO-8601 <c>PT30S</c> and .NET <c>00:00:30</c> both parse via
/// <c>duration.Resolve</c>).
/// </summary>
public static class Default
{
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
    {
        var carrier = new global::app.data.@this("", new global::app.type.item.@null.@this(ctx.Context.App.type.list[new global::app.type.@this("duration", kind), ctx.Context]), context: ctx.Context);
        return global::app.type.item.duration.@this.Create(raw, carrier.Type, carrier);
    }
}
