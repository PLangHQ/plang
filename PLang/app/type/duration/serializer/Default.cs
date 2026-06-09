namespace app.type.duration.serializer;

/// <summary>
/// Read side for <c>duration</c> in the reader registry — the "type reads
/// itself" path. Re-houses the per-family <c>duration.Convert</c> hook
/// (ISO-8601 <c>PT30S</c> and .NET <c>00:00:30</c> both parse via
/// <c>duration.Resolve</c>). The format-layer STJ <c>TimeSpanIso8601</c>
/// converter is a separate concern that stays where its semantics apply
/// (see Documentation/Runtime2/todos.md "Unify TimeSpan's two wire forms").
/// </summary>
public static class Default
{
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
        => global::app.type.duration.@this.Convert(raw, kind, ctx.Context!).Peek();
}
