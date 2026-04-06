using App.Variables;

namespace App.Navigators;

/// <summary>
/// Navigates lists by index and special accessors (.first, .last, .count, .length).
/// </summary>
public sealed class ListNavigator : INavigator
{
    public object? Navigate(Data data, string key)
    {
        var value = data.Value;
        if (value is not System.Collections.IList list) return null;

        // Special accessors
        if (string.Equals(key, "count", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "length", StringComparison.OrdinalIgnoreCase))
            return list.Count;

        if (string.Equals(key, "first", StringComparison.OrdinalIgnoreCase))
            return list.Count > 0 ? list[0] : null;

        if (string.Equals(key, "last", StringComparison.OrdinalIgnoreCase))
            return list.Count > 0 ? list[list.Count - 1] : null;

        if (string.Equals(key, "random", StringComparison.OrdinalIgnoreCase))
            return list.Count > 0 ? list[Random.Shared.Next(list.Count)] : null;

        // Index access
        if (int.TryParse(key, out var index))
        {
            if (index >= 0 && index < list.Count)
                return list[index];
            return null;
        }

        return null;
    }
}
