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

        // Resolve variable references inside bracket indices: [idx] → [1]
        if (name.Contains('['))
            name = ResolveVariablesInPath(name);

        var rootName = GetRootName(name);

        // Simple case: no dot/bracket path — set the root variable directly
        if (rootName == name)
        {
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
            return;
        }

        // Dot/bracket path: navigate to the parent object, then set the final property
        if (!_variables.TryGetValue(rootName, out var root))
        {
            // Root doesn't exist — create it as a dictionary so dot-path properties work
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            root = new Data(rootName, dict);
            root.Context = _context;
            _variables[rootName] = root;
        }

        var remaining = name.Length > rootName.Length && name[rootName.Length] == '.'
            ? name[(rootName.Length + 1)..]
            : name[rootName.Length..];

        // Split remaining into parent path + final property name
        var lastDot = remaining.LastIndexOf('.');
        Data? parent;
        string propertyName;

        if (lastDot >= 0)
        {
            parent = root.GetChild(remaining[..lastDot]);
            propertyName = remaining[(lastDot + 1)..];
        }
        else
        {
            parent = root;
            propertyName = remaining;
        }

        if (parent?.Value == null) return;

        var result = SetValueOnObject(parent.Value, propertyName, value);
        if (!ReferenceEquals(result, parent.Value))
            parent.Value = result;
    }

    /// <summary>
    /// Sets a property on a target object. If the target is a dictionary, sets the key.
    /// If CLR object with writable property, sets via reflection.
    /// Otherwise converts to a case-insensitive dictionary and sets there.
    /// Returns the (possibly replaced) target.
    /// </summary>
    private static object SetValueOnObject(object target, string propertyName, object? value)
    {
        // Dictionary — set key directly (case-insensitive lookup)
        if (target is IDictionary<string, object?> dict)
        {
            var key = dict.Keys.FirstOrDefault(k =>
                string.Equals(k, propertyName, StringComparison.OrdinalIgnoreCase)) ?? propertyName;
            dict[key] = value;
            return target;
        }

        // CLR object — try writable property first
        var prop = target.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(target, value);
            return target;
        }

        // Property is read-only or doesn't exist — convert to dictionary
        var converted = ConvertToDictionary(target);
        var dictKey = converted.Keys.FirstOrDefault(k =>
            string.Equals(k, propertyName, StringComparison.OrdinalIgnoreCase)) ?? propertyName;
        converted[dictKey] = value;
        return converted;
    }

    private static Dictionary<string, object?> ConvertToDictionary(object obj)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in obj.GetType().GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            dict[prop.Name] = prop.GetValue(obj);
        }
        return dict;
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
    /// Recursively resolves %variable% references in any object tree.
    /// Strings: full-match (%var%) returns the actual value, mixed ("hello %name%") interpolates.
    /// Lists: resolves each element. Dicts: resolves each value.
    /// Non-string primitives pass through unchanged.
    /// </summary>
    public object? ResolveDeep(object? value)
    {
        if (value == null) return null;

        if (value is string str)
        {
            if (!str.Contains('%')) return str;

            // Full match: %varName% → return the actual object (not stringified)
            var fullMatch = Regex.Match(str, @"^%([^%]+)%$");
            if (fullMatch.Success)
                return Get(fullMatch.Groups[1].Value)?.Value;

            // Partial match: "hello %name%" → string interpolation
            return Resolve(str);
        }

        if (value is IList<object?> objList)
        {
            var result = new List<object?>(objList.Count);
            foreach (var item in objList)
                result.Add(ResolveDeep(item));
            return result;
        }

        if (value is IDictionary<string, object?> dict)
        {
            var result = new Dictionary<string, object?>(dict.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in dict)
                result[kvp.Key] = ResolveDeep(kvp.Value);
            return result;
        }

        return value;
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

            // SettingsData — share by reference (stateless, loads from DB each time)
            if (kvp.Value is PLang.Runtime2.Engine.Settings.SettingsData)
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
