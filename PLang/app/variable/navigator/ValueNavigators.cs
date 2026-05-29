namespace app.variable.navigator;

/// <summary>
/// Static fallback chain of navigators in priority order.
/// Dictionary → List → JsonString → Object (reflection).
/// Used when no app-level navigator is registered for a type.
/// </summary>
internal static class ValueNavigators
{
    private static readonly INavigator[] _navigators =
    [
        new global::app.variable.navigator.Dictionary(),
        new global::app.variable.navigator.List(),
        new global::app.variable.navigator.JsonString(),
        new global::app.variable.navigator.Object(),
    ];

    public static global::app.data.@this Navigate(global::app.data.@this data, string key)
    {
        foreach (var nav in _navigators)
        {
            if (nav.CanNavigate(data))
                return nav.Navigate(data, key);
        }
        return global::app.data.@this.NotFound(key);
    }
}
