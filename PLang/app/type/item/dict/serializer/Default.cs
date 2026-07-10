namespace app.type.item.dict.serializer;

/// <summary>
/// Reader for <see cref="app.type.item.dict.@this"/> — the Data-free <c>raw → dict</c>
/// deserialize the reader registry dispatches for the <c>dict</c> type. The
/// injected serializer turns wire bytes into the CLR <paramref name="raw"/> (a
/// native dict / wire-shaped object); this turns that into the type instance,
/// the same for every format. Delegates to the type's own
/// <see cref="app.type.item.dict.@this.Convert"/>. Read-only — dict renders through
/// its own json converter.
/// </summary>
public static class Default
{
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
        => global::app.type.item.dict.@this.Create(raw,
            new global::app.data.@this("", new global::app.type.item.@null.@this("dict", kind), context: ctx.Context));
}
