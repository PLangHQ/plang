using System.Collections.Concurrent;
using PLang.Runtime2.Core;
using PLang.Runtime2.Serialization;

namespace PLang.Runtime2.Context;

/// <summary>
/// Application-level context that persists for the lifetime of the PLang application.
/// Shared across all requests/executions.
/// </summary>
public sealed class PLangAppContext : IDisposable
{
    private readonly ConcurrentDictionary<string, object> _data = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>
    /// Unique identifier for this application instance.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The root path of the application.
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// Environment name (e.g., "production", "development").
    /// </summary>
    public string Environment { get; set; }

    /// <summary>
    /// When the application was started.
    /// </summary>
    public DateTime StartedAt { get; }

    /// <summary>
    /// Global event collection for the application.
    /// </summary>
    public Core.Events Events { get; }

    /// <summary>
    /// Serializer registry for the application.
    /// </summary>
    public SerializerRegistry Serializers { get; }

    /// <summary>
    /// Whether debug mode is enabled.
    /// </summary>
    public bool IsDebugMode { get; set; }

    /// <summary>
    /// Cancellation token for graceful shutdown.
    /// </summary>
    public CancellationToken ShutdownToken => _shutdownCts.Token;
    private readonly CancellationTokenSource _shutdownCts = new();

    public PLangAppContext(string rootPath, string? environment = null)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        RootPath = rootPath;
        Environment = environment ?? "production";
        StartedAt = DateTime.UtcNow;
        Events = new Core.Events();
        Serializers = new SerializerRegistry();
    }

    /// <summary>
    /// Gets or sets a value in the application context.
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
    /// Gets a typed value from the application context.
    /// </summary>
    public T? Get<T>(string key)
    {
        if (_data.TryGetValue(key, out var value) && value is T typed)
            return typed;
        return default;
    }

    /// <summary>
    /// Sets a typed value in the application context.
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
    /// Requests graceful shutdown.
    /// </summary>
    public void RequestShutdown()
    {
        _shutdownCts.Cancel();
    }

    /// <summary>
    /// How long the application has been running.
    /// </summary>
    public TimeSpan Uptime => DateTime.UtcNow - StartedAt;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();

        // Dispose any disposable items in the dictionary
        foreach (var value in _data.Values)
        {
            if (value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        _data.Clear();
    }
}
