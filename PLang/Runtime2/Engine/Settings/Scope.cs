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
        throw new NotImplementedException();
    }

    public void Set(string key, object value)
    {
        throw new NotImplementedException();
    }

    public bool Contains(string key)
    {
        throw new NotImplementedException();
    }
}
