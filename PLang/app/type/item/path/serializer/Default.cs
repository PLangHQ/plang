namespace app.type.item.path.serializer;

/// <summary>
/// The path decode, registered by the reader registry. A path's raw wire form is its portable
/// <c>Relative</c> string; with a Context it resolves scheme-correct and fully wired via
/// <see cref="app.type.item.path.@this.Resolve(string, actor.context.@this)"/>. Mid-graph STJ path fields are
/// served by the single json <c>Converter</c>, which routes here through the reader registry. A path writes
/// itself (its as-typed location — the resolved <c>Absolute</c> stays off the wire).
/// </summary>
public static class Default
{
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
    {
        if (raw is not string s || string.IsNullOrEmpty(s)) return null;
        // Born-with-context: the read context always carries the actor scope.
        return global::app.type.item.path.@this.Resolve(s, ctx.Context);
    }
}
