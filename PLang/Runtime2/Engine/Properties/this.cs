using PLang.Runtime2.Engine.Context;
using System.Collections.Concurrent;

namespace PLang.Runtime2.Engine.Properties;

/// <summary>
/// Engine's key-value store. Owns the data dictionary, indexer, Get/Set/Remove,
/// and GoalCall resolution (async Get).
/// </summary>
public sealed class EngineProperty
{
    private readonly ConcurrentDictionary<string, object> _data = new(StringComparer.OrdinalIgnoreCase);
    private readonly Engine _engine;

    public EngineProperty(Engine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Gets or sets a value in the store. Setting null removes the key.
    /// </summary>
    public object? this[string key]
    {
        get => _data.TryGetValue(key, out var value) ? value : null;
        set
        {
            if (value == null)
                _data.TryRemove(key, out _);
            else
                _data[key] = value;
        }
    }

    /// <summary>
    /// Async get — if the value is a GoalCall, executes the goal and returns the result.
    /// </summary>
    public async Task<object?> Get(string key, PLangContext? context = null)
    {
        var value = this[key];
        if (value is GoalCall goalCall)
        {
            context ??= _engine.User.Context;
            var result = await _engine.RunGoalAsync(goalCall, context);
            return result.Success ? result.Value : null;
        }
        return value;
    }

    /// <summary>
    /// Async typed get — if the value is a GoalCall, executes the goal and returns the typed result.
    /// </summary>
    public async Task<T?> Get<T>(string key, PLangContext? context = null)
    {
        var value = await Get(key, context);
        if (value is T typed) return typed;
        return default;
    }

    /// <summary>
    /// Sets a value in the store.
    /// </summary>
    public void Set(string key, object? value)
    {
        this[key] = value;
    }

    /// <summary>
    /// Sets a typed value in the store.
    /// </summary>
    public void Set<T>(string key, T value)
    {
        if (value == null)
            _data.TryRemove(key, out _);
        else
            _data[key] = value;
    }

    /// <summary>
    /// Gets a value or creates it if it doesn't exist.
    /// </summary>
    public T GetOrCreate<T>(string key, Func<T> factory) where T : class
    {
        return (T)_data.GetOrAdd(key, _ => factory()!);
    }

    /// <summary>
    /// Checks if a key exists.
    /// </summary>
    public bool ContainsKey(string key) => _data.ContainsKey(key);

    /// <summary>
    /// Removes a key.
    /// </summary>
    public bool Remove(string key) => _data.TryRemove(key, out _);

    /// <summary>
    /// Gets all keys.
    /// </summary>
    public IEnumerable<string> Keys => _data.Keys;

    /// <summary>
    /// Disposes any IDisposable values and clears the store.
    /// </summary>
    public void DisposeAll()
    {
        foreach (var value in _data.Values)
        {
            if (value is IDisposable disposable)
                disposable.Dispose();
        }
        _data.Clear();
    }
}
