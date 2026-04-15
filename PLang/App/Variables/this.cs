using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Force.DeepCloner;
using App.Actor.Context;

namespace App.Variables;

/// <summary>
/// Thread-safe variable storage for App.
/// </summary>
public class @this
{
    private readonly ConcurrentDictionary<string, Data.@this> _variables = new(StringComparer.OrdinalIgnoreCase);
    private Actor.Context.@this? _context;

    [JsonIgnore]
    internal Actor.Context.@this? Context
    {
        get => _context;
        set
        {
            _context = value;
            foreach (var data in _variables.Values)
                data.Context = value;
        }
    }

    public @this()
    {
        // Register system variables
        Put(new Data.DynamicData("Now", () => DateTimeOffset.Now, Data.Type.DateTime));
        Put(new Data.DynamicData("NowUtc", () => DateTimeOffset.UtcNow, Data.Type.DateTime));
        Put(new Data.DynamicData("GUID", () => Guid.NewGuid(), Data.Type.FromName("guid")));
    }

    /// <summary>
    /// Stores or updates a variable.
    /// </summary>
    public void Put(Data.@this value)
    {
        value.Context = _context;
        _variables[value.Name] = value;
    }

    /// <summary>
    /// Stores a value with the given name.
    /// </summary>
    public void Set(string name, object? value, Data.Type? type = null)
    {
        name = CleanName(name);

        // Resolve variable references inside bracket indices: [idx] → [1]
        if (name.Contains('['))
            name = ResolveVariablesInPath(name);

        var rootName = GetRootName(name);

        // Simple case: no dot/bracket path — set the root variable directly
        if (rootName == name)
        {
            // Store Data values directly — rename to match the variable name
            if (value is Data.@this dv)
            {
                dv.Name = name;
                if (type != null) dv.Type = type;
                dv.Context = _context;

                if (_variables.TryGetValue(name, out var prev))
                {
                    prev.FireOnChange(dv);
                    dv.CopyEventsFrom(prev);
                }
                else
                {
                    dv.FireOnCreate();
                }

                _variables[name] = dv;
                return;
            }

            if (_variables.TryGetValue(name, out var existing))
            {
                existing.Value = value;
                if (type != null)
                    existing.Type = type;
            }
            else
            {
                var data = new Data.@this(name, value, type);
                data.Context = _context;
                data.FireOnCreate();
                _variables[name] = data;
            }
            return;
        }

        // Dot/bracket path: navigate to the parent object, set the property with raw value
        if (!_variables.TryGetValue(rootName, out var root))
        {
            // Root doesn't exist — create it as a dictionary so dot-path properties work
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            root = new Data.@this(rootName, dict);
            root.Context = _context;
            _variables[rootName] = root;
        }

        var remaining = name.Length > rootName.Length && name[rootName.Length] == '.'
            ? name[(rootName.Length + 1)..]
            : name[rootName.Length..];

        // Split remaining into parent path + final property name
        var lastDot = remaining.LastIndexOf('.');
        Data.@this parent;
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

        if (!parent.IsInitialized && parent.Value == null) return;

        // Lazy convert if parent is a typed string (e.g., json) — must happen before navigation
        parent.ConvertValue();

        // For dot-path, extract raw value from Data — we're setting a property on a C# object
        var rawValue = value is Data.@this dv2 ? dv2.Value : value;
        var target = parent.Value;
        if (target == null) return;
        var result = SetValueOnObject(target, propertyName, rawValue);
        if (!ReferenceEquals(result, target))
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

        // Handle bracket indexing: "Steps[0]" → property "Steps", index 0
        var bracketIdx = propertyName.IndexOf('[');
        if (bracketIdx > 0)
        {
            var baseProp = propertyName[..bracketIdx];
            var indexStr = propertyName[(bracketIdx + 1)..].TrimEnd(']');
            var prop = target.GetType().GetProperty(baseProp,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (prop != null)
            {
                var collection = prop.GetValue(target);
                if (collection is System.Collections.IList list && int.TryParse(indexStr, out var idx) && idx >= 0 && idx < list.Count)
                {
                    if (value != null)
                    {
                        var elementType = list.GetType().IsGenericType
                            ? list.GetType().GetGenericArguments()[0]
                            : typeof(object);
                        if (!elementType.IsAssignableFrom(value.GetType()))
                        {
                            var (typedValue, _) = Utils.TypeMapping.TryConvertTo(value, elementType);
                            if (typedValue != null) value = typedValue;
                        }
                    }
                    list[idx] = value;
                    return target;
                }
                // Generic IList<T> (e.g., Steps.@this, Actions.@this) — use indexer via reflection
                else if (collection != null && int.TryParse(indexStr, out var gIdx) && gIdx >= 0)
                {
                    var indexer = collection.GetType().GetProperty("Item");
                    var countProp = collection.GetType().GetProperty("Count");
                    if (indexer != null && countProp != null)
                    {
                        var count = (int)countProp.GetValue(collection)!;
                        if (gIdx < count)
                        {
                            if (value != null && !indexer.PropertyType.IsAssignableFrom(value.GetType()))
                            {
                                var (typedValue, _) = Utils.TypeMapping.TryConvertTo(value, indexer.PropertyType);
                                if (typedValue != null) value = typedValue;
                            }
                            indexer.SetValue(collection, value, new object[] { gIdx });
                            return target;
                        }
                    }
                }
            }
        }

        // CLR object — try writable property first
        var clrProp = target.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (clrProp != null && clrProp.CanWrite)
        {
            if (value != null && !clrProp.PropertyType.IsAssignableFrom(value.GetType()))
            {
                var (typedValue, _) = Utils.TypeMapping.TryConvertTo(value, clrProp.PropertyType);
                if (typedValue != null) value = typedValue;
            }
            clrProp.SetValue(target, value);
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
        var props = obj.GetType().GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        foreach (var prop in props)
        {
            if (prop.GetIndexParameters().Length > 0) continue; // skip indexers
            dict[prop.Name] = prop.GetValue(obj);
        }
        // Primitive/value type with no navigable properties — preserve original value
        if (dict.Count == 0)
            dict["value"] = obj;
        return dict;
    }

    /// <summary>
    /// Gets a variable by name (supports dot notation path).
    /// </summary>
    public Data.@this Get(string name)
    {
        if (string.IsNullOrEmpty(name))
            return Data.@this.NotFound(name ?? "");

        name = CleanName(name);

        // Resolve variable references inside bracket indices: [idx] → [1]
        if (name.Contains('['))
            name = ResolveVariablesInPath(name);

        // Handle paths like "user.name" or "items[0].value"
        var rootName = GetRootName(name);
        string? remaining;
        if (name.Length > rootName.Length)
        {
            var sep = name[rootName.Length];
            // Strip leading . (but keep ! as it's the infrastructure marker for GetChild)
            remaining = sep == '.'
                ? name[(rootName.Length + 1)..]
                : name[rootName.Length..];
        }
        else
        {
            remaining = null;
        }

        if (!_variables.TryGetValue(rootName, out var root))
        {
            return Data.@this.NotFound(name);
        }

        if (string.IsNullOrEmpty(remaining))
            return root;

        var child = root.GetChild(remaining);
        return child;
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
    /// Returns variables that changed since the given time. Uses Data.Updated timestamp.
    /// </summary>
    public Dictionary<string, string> GetChangedSince(DateTime since)
    {
        var result = new Dictionary<string, string>();
        foreach (var (name, data) in _variables)
        {
            if (name.StartsWith("!")) continue; // skip system variables
            if (data.Updated > since)
                result[name] = data.Value?.ToString() ?? "(null)";
        }
        return result;
    }

    /// <summary>
    /// Resolves %variable% references in a string using this Variables instance.
    /// Returns the input unchanged if no %var% patterns are found.
    /// When <paramref name="skipInfrastructure"/> is true, %!variable% references (infrastructure
    /// variables like %!app%, %!callStack%) are left unresolved. Use this for untrusted input
    /// (e.g., file content, HTTP responses) to prevent information disclosure.
    /// </summary>
    public string Resolve(string input, bool skipInfrastructure = false)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains('%'))
            return input;

        return Regex.Replace(input, @"%([^%]+)%", match =>
        {
            var varName = match.Groups[1].Value;
            if (skipInfrastructure && varName.StartsWith('!'))
                return match.Value; // Leave %!var% unresolved for untrusted input
            return Get(varName)?.Value?.ToString() ?? match.Value;
        });
    }

    /// <summary>
    /// Recursively resolves %variable% references in any object tree.
    /// Strings: full-match (%var%) returns the actual value, mixed ("hello %name%") interpolates.
    /// Lists: resolves each element. Dicts: resolves each value.
    /// Non-string primitives pass through unchanged.
    /// </summary>
    [ThreadStatic] private static int _resolveDepth;

    public object? ResolveDeep(object? value)
    {
        if (value == null) return null;

        _resolveDepth++;
        if (_resolveDepth > 50)
        {
            var valueType = value?.GetType().FullName ?? "null";
            Console.Error.WriteLine($"ResolveDeep depth={_resolveDepth}, type={valueType}, value={value?.ToString()?[..Math.Min(value.ToString()!.Length, 100)]}");
            if (_resolveDepth > 100)
            {
                Console.Error.WriteLine("ResolveDeep OVERFLOW — aborting");
                _resolveDepth--;
                return value;
            }
        }
        try
        {

        // Don't recurse into Data objects — their Value getter calls ResolveDeep itself
        if (value is Data.@this) return value;
        // Don't recurse into JsonElement — it's immutable and doesn't contain %var% references
        if (value is System.Text.Json.JsonElement) return value;

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

        // Typed lists (e.g., List<LlmMessage>) — resolve each item in place
        if (value is System.Collections.IList typedList && value is not string)
        {
            for (int i = 0; i < typedList.Count; i++)
                ResolveDeep(typedList[i]);
            return value;
        }

        if (value is IDictionary<string, object?> dict)
        {
            var result = new Dictionary<string, object?>(dict.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in dict)
                result[kvp.Key] = ResolveDeep(kvp.Value);
            return result;
        }

        // Typed objects: resolve %var% only in string properties (no deep recursion to avoid cycles)
        var type = value.GetType();
        if (!type.IsPrimitive && type != typeof(decimal) && type != typeof(DateTime)
            && type != typeof(DateTimeOffset) && type != typeof(Guid) && !type.IsEnum)
        {
            foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (!prop.CanRead || !prop.CanWrite) continue;
                if (prop.PropertyType != typeof(string)) continue;

                var strValue = prop.GetValue(value) as string;
                if (strValue == null || !strValue.Contains('%')) continue;

                var resolved = ResolveDeep(strValue);
                if (!ReferenceEquals(resolved, strValue))
                    prop.SetValue(value, resolved);
            }
        }

        return value;

        }
        finally { _resolveDepth--; }
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
    public IEnumerable<Data.@this> GetAll()
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
            .Where(kvp => !kvp.Key.StartsWith("!") && kvp.Value is not Data.DynamicData)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            _variables.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Creates a deep clone of this Variables instance.
    /// Values are deep-cloned so mutations in the clone do not affect the original.
    /// </summary>
    public @this Clone()
    {
        var clone = new @this();
        foreach (var kvp in _variables)
        {
            // Data.DynamicData (Now, GUID, etc.) — already in clone from constructor
            if (kvp.Value is Data.DynamicData) continue;

            // System context vars (! prefix) — skip, they're per-execution
            if (kvp.Key.StartsWith("!")) continue;

            // SettingsVariable — share by reference (stateless, loads from DB each time)
            if (kvp.Value is App.Settings.SettingsVariable)
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
    /// Saves a snapshot of current variable keys for later restore.
    /// </summary>
    public HashSet<string> Save() => new HashSet<string>(_variables.Keys, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Restores to a saved snapshot: removes any variables added after the snapshot.
    /// </summary>
    public void Restore(HashSet<string> snapshot)
    {
        foreach (var key in _variables.Keys)
        {
            if (!snapshot.Contains(key))
                _variables.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Converts Variables to a dictionary (for serialization/debugging).
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
        var bangIndex = path.IndexOf('!');
        // ! at position 0 is part of the variable name (!app), not a separator
        if (bangIndex == 0) bangIndex = path.IndexOf('!', 1);

        // Find the earliest separator
        int min = int.MaxValue;
        if (dotIndex >= 0) min = Math.Min(min, dotIndex);
        if (bracketIndex >= 0) min = Math.Min(min, bracketIndex);
        if (bangIndex > 0) min = Math.Min(min, bangIndex);

        return min == int.MaxValue ? path : path[..min];
    }
}

/// <summary>
/// Provides async-local access to Variables.
/// </summary>
public interface IVariablesAccessor
{
    Variables.@this Current { get; set; }
}

/// <summary>
/// Default implementation using AsyncLocal.
/// </summary>
public class @thisAccessor : IVariablesAccessor
{
    private static readonly AsyncLocal<@this> _current = new();

    public Variables.@this Current
    {
        get => _current.Value ?? (_current.Value = new @this());
        set => _current.Value = value;
    }
}
