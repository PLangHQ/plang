using System.Collections.Concurrent;

namespace PLang.Runtime2.Memory;

/// <summary>
/// Thread-safe variable storage for Runtime2.
/// </summary>
public class MemoryStack
{
    private readonly ConcurrentDictionary<string, Data> _variables = new(StringComparer.OrdinalIgnoreCase);

    public MemoryStack()
    {
        // Register system variables
        Put(new DynamicData("Now", () => DateTime.Now, Type.DateTime));
        Put(new DynamicData("NowUtc", () => DateTime.UtcNow, Type.DateTime));
        Put(new DynamicData("GUID", () => Guid.NewGuid(), Type.FromName("guid")));
    }

    /// <summary>
    /// Stores or updates a variable.
    /// </summary>
    public void Put(Data value)
    {
        _variables[value.Name] = value;
    }

    /// <summary>
    /// Stores a value with the given name.
    /// </summary>
    public void Set(string name, object? value, Type? type = null)
    {
        name = CleanName(name);
        if (_variables.TryGetValue(name, out var existing))
        {
            existing.Value = value;
            if (type != null)
                existing.Type = type;
        }
        else
        {
            _variables[name] = new Data(name, value, type);
        }
    }

    /// <summary>
    /// Gets a variable by name (supports dot notation path).
    /// </summary>
    public Data? Get(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        name = CleanName(name);

        // Handle paths like "user.name" or "items[0].value"
        var rootName = GetRootName(name);
        var remaining = name.Length > rootName.Length ? name[(rootName.Length + 1)..] : null;

        if (!_variables.TryGetValue(rootName, out var root))
            return null;

        if (string.IsNullOrEmpty(remaining))
            return root;

        return root.GetChild(remaining);
    }

    /// <summary>
    /// Gets a typed value by name.
    /// </summary>
    public T? Get<T>(string name)
    {
        var ov = Get(name);
        return ov != null ? ov.GetValue<T>() : default;
    }

    /// <summary>
    /// Gets a value by name, returning the raw value or null.
    /// </summary>
    public object? GetValue(string name)
    {
        var ov = Get(name);
        return ov?.Value;
    }

    /// <summary>
    /// Checks if a variable exists.
    /// </summary>
    public bool Contains(string name)
    {
        name = CleanName(name);
        return _variables.ContainsKey(GetRootName(name));
    }

    /// <summary>
    /// Removes a variable.
    /// </summary>
    public bool Remove(string name)
    {
        name = CleanName(name);
        return _variables.TryRemove(name, out _);
    }

    /// <summary>
    /// Gets all variable names.
    /// </summary>
    public IEnumerable<string> GetNames()
    {
        return _variables.Keys.Where(k => !k.StartsWith("!"));
    }

    /// <summary>
    /// Gets all variables ordered by last update.
    /// </summary>
    public IEnumerable<Data> GetAll()
    {
        return _variables.Values
            .Where(v => !v.Name.StartsWith("!"))
            .OrderByDescending(v => v.Updated);
    }

    /// <summary>
    /// Clears all non-system variables.
    /// </summary>
    public void Clear()
    {
        var systemVars = _variables.Where(kvp => kvp.Key.StartsWith("!") ||
            kvp.Key.Equals("Now", StringComparison.OrdinalIgnoreCase) ||
            kvp.Key.Equals("NowUtc", StringComparison.OrdinalIgnoreCase) ||
            kvp.Key.Equals("GUID", StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .ToList();

        var toRemove = _variables.Keys.Except(systemVars).ToList();
        foreach (var key in toRemove)
        {
            _variables.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Creates a shallow clone of this memory stack.
    /// </summary>
    public MemoryStack Clone()
    {
        var clone = new MemoryStack();
        foreach (var kvp in _variables)
        {
            // Clone non-system variables
            if (!kvp.Key.Equals("Now", StringComparison.OrdinalIgnoreCase) &&
                !kvp.Key.Equals("NowUtc", StringComparison.OrdinalIgnoreCase) &&
                !kvp.Key.Equals("GUID", StringComparison.OrdinalIgnoreCase))
            {
                clone._variables[kvp.Key] = new Data(kvp.Value.Name, kvp.Value.Value, kvp.Value.Type);
            }
        }
        return clone;
    }

    /// <summary>
    /// Converts the memory stack to a dictionary (for serialization/debugging).
    /// </summary>
    public Dictionary<string, object?> ToDictionary(bool includeSystem = false)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _variables)
        {
            if (!includeSystem && kvp.Key.StartsWith("!"))
                continue;
            dict[kvp.Key] = kvp.Value.Value;
        }
        return dict;
    }

    private static string CleanName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        return name.Trim().TrimStart('%').TrimEnd('%');
    }

    private static string GetRootName(string path)
    {
        var dotIndex = path.IndexOf('.');
        var bracketIndex = path.IndexOf('[');

        if (dotIndex < 0 && bracketIndex < 0)
            return path;

        if (dotIndex < 0)
            return path[..bracketIndex];
        if (bracketIndex < 0)
            return path[..dotIndex];

        return path[..Math.Min(dotIndex, bracketIndex)];
    }
}

/// <summary>
/// Provides async-local access to MemoryStack.
/// </summary>
public interface IMemoryStackAccessor
{
    MemoryStack Current { get; set; }
}

/// <summary>
/// Default implementation using AsyncLocal.
/// </summary>
public class MemoryStackAccessor : IMemoryStackAccessor
{
    private static readonly AsyncLocal<MemoryStack> _current = new();

    public MemoryStack Current
    {
        get => _current.Value ?? (_current.Value = new MemoryStack());
        set => _current.Value = value;
    }
}
