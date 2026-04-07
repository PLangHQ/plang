namespace App.Data.Navigators;

/// <summary>
/// Static fallback chain of navigators in priority order.
/// Dictionary → List → JsonString → Object (reflection).
/// Used when no app-level navigator is registered for a type.
/// </summary>
internal static class ValueNavigators
{
    private static readonly INavigator[] _navigators =
    [
        new DictionaryNavigator(),
        new ListNavigator(),
        new JsonStringNavigator(),
        new ObjectNavigator(),
    ];

    public static Data.@this Navigate(Data.@this data, string key)
    {
        foreach (var nav in _navigators)
        {
            if (nav.CanNavigate(data))
                return nav.Navigate(data, key);
        }
        return Data.@this.Null(key);
    }
}
