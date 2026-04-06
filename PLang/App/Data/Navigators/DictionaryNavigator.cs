using App.Variables;

namespace App.Data.Navigators;

/// <summary>
/// Navigates dictionaries by case-insensitive key lookup.
/// </summary>
public sealed class DictionaryNavigator : INavigator
{
    public object? Navigate(@this data, string key)
    {
        var value = data.Value;

        if (value is IDictionary<string, object?> dict)
        {
            // Case-insensitive key lookup
            foreach (var kvp in dict)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }
            return null;
        }

        if (value is System.Collections.IDictionary rawDict)
        {
            foreach (System.Collections.DictionaryEntry entry in rawDict)
            {
                if (entry.Key is string k && string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                    return entry.Value;
            }
            return null;
        }

        return null;
    }
}
