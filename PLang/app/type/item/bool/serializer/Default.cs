namespace app.type.item.@bool.serializer;

/// <summary>
/// Reader for <see cref="app.type.item.@bool.@this"/> — the Data-free <c>raw → bool</c>
/// deserialize the reader registry dispatches for the <c>bool</c> type. The
/// injected serializer turns wire bytes into the CLR <paramref name="raw"/> (a
/// bool or its string form); this turns that into the type instance, the same
/// for every format. Read-only — bool renders through its own json converter.
/// </summary>
public static class Default
{
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
        => raw switch
        {
            null => null,
            global::app.type.item.@bool.@this b => b,
            bool b => new global::app.type.item.@bool.@this(b),
            string s when bool.TryParse(s, out var parsed) => new global::app.type.item.@bool.@this(parsed),
            _ => null,
        };
}
