using System.Collections;

namespace app.Variables.Navigators;

/// <summary>
/// Navigates dictionaries by case-insensitive key lookup. Three cases, in order
/// of specificity:
///  1. <c>IDictionary&lt;string, object?&gt;</c> — the canonical PLang dict shape.
///  2. <c>IDictionary</c> (non-generic) — legacy/foreign dicts; we walk via DictionaryEntry.
///  3. Any <c>IDictionary&lt;string, T&gt;</c> for arbitrary <c>T</c> — reached via the navigator
///     registry's <c>IDictionary&lt;,&gt;</c> generic match. <see cref="System.Text.Json.Nodes.JsonObject"/>
///     is the load-bearing case: it implements <c>IDictionary&lt;string, JsonNode?&gt;</c>, NOT
///     <c>IDictionary&lt;string, object?&gt;</c> and not non-generic <c>IDictionary</c>, so without
///     this third arm dot-path navigation through `set ... type=json` values fell through to
///     the reflection navigator and only ever found CLR properties (Count, Options, Parent,
///     Root) — never the actual JSON keys.
/// </summary>
public sealed class Dictionary : INavigator
{
    public bool CanNavigate(Data.@this data)
    {
        var v = data.Value;
        if (v is null) return false;
        if (v is IDictionary<string, object?>) return true;
        if (v is IDictionary) return true;
        return GetStringKeyedDictType(v) != null;
    }

    public Data.@this Navigate(Data.@this data, string key)
    {
        var value = data.Value;
        if (value == null) return Data.@this.NotFound(key);

        // Rule across all three arms: user keys win — "Count" only resolves to dict.Count
        // when no key with that name exists. Otherwise a dict literal `{count: "x"}`
        // would silently expose the dict's length instead of "x", and the answer would
        // depend on which IDictionary shape the value implements.
        if (value is IDictionary<string, object?> generic)
        {
            foreach (var kvp in generic)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                    return new Data.@this(key, kvp.Value, parent: data);
            }
            if (string.Equals(key, "Count", StringComparison.OrdinalIgnoreCase))
                return new Data.@this(key, generic.Count, parent: data);
            return Data.@this.NotFound(key);
        }

        if (value is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                if (entry.Key is string k && string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                    return new Data.@this(key, entry.Value, parent: data);
            }
            if (string.Equals(key, "Count", StringComparison.OrdinalIgnoreCase))
                return new Data.@this(key, dict.Count, parent: data);
            return Data.@this.NotFound(key);
        }

        // IDictionary<string, T> for arbitrary T (JsonObject and friends).
        // Walk via IEnumerable<KeyValuePair<string, T>>: every IDictionary<TK,TV> implements
        // IEnumerable<KVP<TK,TV>>, and KVP exposes Key/Value through reflection without
        // needing T at compile time.
        if (GetStringKeyedDictType(value) != null)
        {
            int count = 0;
            foreach (var entry in (IEnumerable)value)
            {
                count++;
                if (entry == null) continue;
                var entryType = entry.GetType();
                var keyProp = entryType.GetProperty("Key");
                var valProp = entryType.GetProperty("Value");
                if (keyProp?.GetValue(entry) is string k
                    && string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                    return new Data.@this(key, valProp?.GetValue(entry), parent: data);
            }
            if (string.Equals(key, "Count", StringComparison.OrdinalIgnoreCase))
                return new Data.@this(key, count, parent: data);
            return Data.@this.NotFound(key);
        }

        return Data.@this.NotFound(key);
    }

    /// <summary>
    /// Returns the implemented <c>IDictionary&lt;string, T&gt;</c> type if <paramref name="value"/>
    /// has one, else null. Used to recognize JsonObject (T = JsonNode?) and any other foreign
    /// string-keyed generic dictionary that doesn't fit the standard interfaces.
    /// </summary>
    private static System.Type? GetStringKeyedDictType(object value)
    {
        foreach (var iface in value.GetType().GetInterfaces())
        {
            if (!iface.IsGenericType) continue;
            if (iface.GetGenericTypeDefinition() != typeof(IDictionary<,>)) continue;
            if (iface.GetGenericArguments()[0] == typeof(string)) return iface;
        }
        return null;
    }
}
