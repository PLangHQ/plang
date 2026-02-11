using System.Collections;

namespace PLang.Runtime2.Memory.Navigators;

/// <summary>
/// Navigates IDictionary&lt;string, object?&gt; and IDictionary by key lookup.
/// </summary>
public class DictionaryNavigator : IValueNavigator
{
    public bool CanNavigate(object value)
        => value is IDictionary<string, object?> or IDictionary;

    public object? GetProperty(object value, string key)
    {
        if (value is IDictionary<string, object?> generic)
        {
            // Case-insensitive key lookup
            foreach (var kvp in generic)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }
            return null;
        }

        if (value is IDictionary dict)
        {
            return dict.Contains(key) ? dict[key] : null;
        }

        return null;
    }
}
