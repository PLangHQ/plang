using System.Collections;

namespace App.Data.Navigators;

/// <summary>
/// Navigates dictionaries by case-insensitive key lookup.
/// Handles IDictionary&lt;string, object?&gt; and IDictionary.
/// </summary>
public sealed class DictionaryNavigator : INavigator
{
    public bool CanNavigate(Data.@this data)
        => data.Value is IDictionary<string, object?> or IDictionary;

    public Data.@this Navigate(Data.@this data, string key)
    {
        var value = data.Value;

        if (value is IDictionary<string, object?> generic)
        {
            foreach (var kvp in generic)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                    return new Data.@this(key, kvp.Value, parent: data);
            }
            return Data.@this.NotFound(key);
        }

        if (value is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                if (entry.Key is string k && string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                    return new Data.@this(key, entry.Value, parent: data);
            }
            return Data.@this.NotFound(key);
        }

        return Data.@this.NotFound(key);
    }
}
