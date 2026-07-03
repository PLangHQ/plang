namespace app.type.dict.serializer;

/// <summary>
/// Reader for <see cref="app.type.dict.@this"/> — the Data-free <c>raw → dict</c>
/// deserialize the reader registry dispatches for the <c>dict</c> type. The
/// injected serializer turns wire bytes into the CLR <paramref name="raw"/> (a
/// native dict / wire-shaped object); this turns that into the type instance,
/// the same for every format. Delegates to the type's own
/// <see cref="app.type.dict.@this.Convert"/>. Read-only — dict renders through
/// its own json converter.
/// </summary>
public static class Default
{
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
        => global::app.type.dict.@this.Convert(raw, kind, ctx.Context)?.Peek();
}
