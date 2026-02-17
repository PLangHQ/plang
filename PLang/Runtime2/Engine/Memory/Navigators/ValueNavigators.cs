namespace PLang.Runtime2.Engine.Memory.Navigators;

/// <summary>
/// Static registry of navigators in priority order.
/// Dictionary → List → JsonString → Object (reflection).
/// </summary>
internal static class ValueNavigators
{
    private static readonly IValueNavigator[] _navigators =
    [
        new DictionaryNavigator(),
        new ListNavigator(),
        new JsonStringNavigator(),
        new ObjectNavigator(),
    ];

    public static object? Navigate(object value, string key)
    {
        foreach (var nav in _navigators)
        {
            if (nav.CanNavigate(value))
                return nav.GetProperty(value, key);
        }
        return null;
    }
}
