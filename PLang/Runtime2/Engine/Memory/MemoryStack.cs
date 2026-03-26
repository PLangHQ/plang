using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Force.DeepCloner;
using PLang.Runtime2.Engine.Context;

namespace PLang.Runtime2.Engine.Memory;

/// <summary>
/// Thread-safe variable storage for Runtime2.
/// </summary>
public class MemoryStack
{
    private readonly ConcurrentDictionary<string, Data> _variables = new(StringComparer.OrdinalIgnoreCase);
    private PLangContext? _context;

    [JsonIgnore]
    internal PLangContext? Context
    {
        get => _context;
        set
        {
            _context = value;
            foreach (var data in _variables.Values)
                data.Context = value;
        }
    }

    public MemoryStack()
    {
        // Register system variables
        Put(new DynamicData("Now", () => DateTimeOffset.Now, Type.DateTime));
        Put(new DynamicData("NowUtc", () => DateTimeOffset.UtcNow, Type.DateTime));
        Put(new DynamicData("GUID", () => Guid.NewGuid(), Type.FromName("guid")));
    }

    /// <summary>
    /// Stores or updates a variable.
    /// </summary>
    public void Put(Data value)
    {
        value.Context = _context;
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
            var data = new Data(name, value, type);
            data.Context = _context;
            _variables[name] = data;
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

        // Resolve variable references inside bracket indices: [idx] → [1]
        if (name.Contains('['))
            name = ResolveVariablesInPath(name);

        // Handle paths like "user.name" or "items[0].value"
        var rootName = GetRootName(name);
        string? remaining;
        if (name.Length > rootName.Length)
        {
            remaining = name[rootName.Length] == '.'
                ? name[(rootName.Length + 1)..]
                : name[rootName.Length..];
        }
        else
        {
            remaining = null;
        }

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
    /// Resolves %variable% references in a string using this memory stack.
    /// Returns the input unchanged if no %var% patterns are found.
    /// </summary>
    public string Resolve(string input)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains('%'))
            return input;

        return Regex.Replace(input, @"%([^%]+)%", match =>
        {
            var varName = match.Groups[1].Value;
            return Get(varName)?.Value?.ToString() ?? match.Value;
        });
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
        var toRemove = _variables
            .Where(kvp => !kvp.Key.StartsWith("!") && kvp.Value is not DynamicData)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            _variables.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Creates a deep clone of this memory stack.
    /// Values are deep-cloned so mutations in the clone do not affect the original.
    /// </summary>
    public MemoryStack Clone()
    {
        var clone = new MemoryStack();
        foreach (var kvp in _variables)
        {
            // DynamicData (Now, GUID, etc.) — already in clone from constructor
            if (kvp.Value is DynamicData) continue;

            // System context vars (! prefix) — skip, they're per-execution
            if (kvp.Key.StartsWith("!")) continue;

            // Specialized Data subclasses (SettingsData, DynamicData) — share by reference.
            // They're stateless (load from DB each time) and deep-cloning would lose
            // the virtual GetChild override.
            if (kvp.Value.GetType() != typeof(Data))
            {
                clone._variables[kvp.Key] = kvp.Value;
            }
            else
            {
                clone._variables[kvp.Key] = kvp.Value.Clone();
            }
        }
        clone.Context = Context;
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

    [ThreadStatic]
    private static HashSet<string>? _resolvingVars;

    /// <summary>
    /// Resolves variable names inside bracket indices.
    /// e.g. "addresses[idx].street" with idx=1 → "addresses[1].street"
    /// Uses a thread-static visited set to detect circular references (a→b→a).
    /// </summary>
    private string ResolveVariablesInPath(string path)
    {
        var isRoot = _resolvingVars == null;
        _resolvingVars ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            return Regex.Replace(path, @"\[([^\]\d][^\]]*)\]", match =>
            {
                var varName = match.Groups[1].Value;
                if (!_resolvingVars!.Add(varName))
                    return match.Value; // Circular reference — leave unresolved
                try
                {
                    var resolved = GetValue(varName);
                    return resolved != null ? $"[{resolved}]" : match.Value;
                }
                finally
                {
                    _resolvingVars.Remove(varName);
                }
            });
        }
        finally
        {
            if (isRoot) _resolvingVars = null;
        }
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
