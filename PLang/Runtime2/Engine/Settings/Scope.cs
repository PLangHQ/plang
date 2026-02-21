using System.Collections.Concurrent;

namespace PLang.Runtime2.Engine.Settings;

/// <summary>
/// One level of the settings scope chain — a key-value store for settings
/// set within a single goal execution. Keys are "module.property" format
/// (e.g., "archive.max"). Thread-safe via ConcurrentDictionary.
/// </summary>
public sealed class Scope
{
    private readonly ConcurrentDictionary<string, object> _values =
        new(StringComparer.OrdinalIgnoreCase);

    public object? Get(string key)
    {
        return _values.TryGetValue(key, out var value) ? value : null;
    }

    public void Set(string key, object value)
    {
        if (value == null) { _values.TryRemove(key, out _); return; }
        _values[key] = value;
    }

    public bool Contains(string key)
    {
        return _values.ContainsKey(key);
    }
}
