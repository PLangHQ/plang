namespace app.variable.navigator;

/// <summary>
/// Static fallback chain of navigators in priority order.
/// Dictionary → List → Object (reflection).
/// Used when no app-level navigator is registered for a type.
///
/// There is deliberately no json-string navigator: navigating a value means
/// its type says how to read it (a typed json value materializes to a dict
/// through the reader before reaching here). Sniffing a bare string for a
/// leading `{`/`[` and parsing it would be a content guess — forbidden by
/// access-driven resolution. An untyped string navigated by key errors with
/// "add `as <type>`" instead.
/// </summary>
internal static class ValueNavigators
{
    private static readonly INavigator[] _navigators =
    [
        new global::app.variable.navigator.Dictionary(),
        new global::app.variable.navigator.List(),
        new global::app.variable.navigator.Object(),
    ];

    public static async System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(global::app.data.@this data, string key)
    {
        foreach (var nav in _navigators)
        {
            if (nav.CanNavigate(data))
                return await nav.Navigate(data, key);
        }
        return global::app.data.@this.NotFound(key);
    }
}
