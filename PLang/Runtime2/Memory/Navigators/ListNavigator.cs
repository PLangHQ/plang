using System.Collections;

namespace PLang.Runtime2.Memory.Navigators;

/// <summary>
/// Navigates IList by numeric index, named accessors (first/last/random),
/// or implicit first element delegation.
/// </summary>
public class ListNavigator : IValueNavigator
{
    public bool CanNavigate(object value)
        => value is IList;

    public object? GetProperty(object value, string key)
    {
        if (value is not IList list || list.Count == 0)
            return null;

        // Numeric index
        if (int.TryParse(key, out var index))
        {
            return index >= 0 && index < list.Count ? list[index] : null;
        }

        // Named accessors
        if (string.Equals(key, "first", StringComparison.OrdinalIgnoreCase))
            return list[0];

        if (string.Equals(key, "last", StringComparison.OrdinalIgnoreCase))
            return list[list.Count - 1];

        if (string.Equals(key, "random", StringComparison.OrdinalIgnoreCase))
            return list[Random.Shared.Next(list.Count)];

        if (string.Equals(key, "count", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "length", StringComparison.OrdinalIgnoreCase))
            return list.Count;

        // Implicit first: delegate to first element's navigator
        // e.g. %addresses.street% → addresses[0].street
        return ValueNavigators.Navigate(list[0]!, key);
    }
}
