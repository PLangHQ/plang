namespace app.type.item.list.serializer;

/// <summary>
/// Reader for <see cref="app.type.item.list.@this"/> — the Data-free <c>raw → list</c>
/// deserialize the reader registry dispatches for the <c>list</c> type. The
/// injected serializer turns wire bytes into the CLR <paramref name="raw"/> (a
/// native list / wire-shaped array); this turns that into the type instance, the
/// same for every format. Delegates to the type's own
/// <see cref="app.type.item.list.@this.Convert"/>. Read-only — list renders through
/// its own json converter.
/// </summary>
public static class Default
{
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
        => global::app.type.item.list.@this.Create(raw,
            new global::app.data.@this("", new global::app.type.item.@null.@this("list", kind), context: ctx.Context));
}
