using System.Collections.Concurrent;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Context;

/// <summary>
/// Request-level context for a single PLang execution.
/// Created per request/goal execution and contains execution-specific state.
/// </summary>
public sealed class PLangContext : IDisposable
{
    private readonly ConcurrentDictionary<string, object> _data = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>
    /// Unique identifier for this execution context.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Reference to the application context.
    /// </summary>
    public PLangAppContext AppContext { get; }

    /// <summary>
    /// Memory stack for this execution.
    /// </summary>
    public MemoryStack MemoryStack { get; }

    /// <summary>
    /// Call stack for this execution.
    /// </summary>
    public CallStack? CallStack { get; set; }

    /// <summary>
    /// Whether this is an async execution.
    /// </summary>
    public bool IsAsync { get; set; }

    /// <summary>
    /// The goal being executed (if any).
    /// </summary>
    public string? CurrentGoalName { get; set; }

    /// <summary>
    /// The step being executed (if any).
    /// </summary>
    public int? CurrentStepIndex { get; set; }

    /// <summary>
    /// When this context was created.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Cancellation token for this execution.
    /// </summary>
    public CancellationToken CancellationToken => _cts?.Token ?? CancellationToken.None;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Parent context (if this is a child execution).
    /// </summary>
    public PLangContext? Parent { get; }

    /// <summary>
    /// Execution depth (0 for root, increments for nested calls).
    /// </summary>
    public int Depth { get; }

    /// <summary>
    /// The actor that owns this context (if any).
    /// </summary>
    public Actor? Actor { get; internal set; }

    /// <summary>
    /// System-level event scope.
    /// </summary>
    public EventScope System { get; }

    /// <summary>
    /// User-level event scope.
    /// </summary>
    public EventScope User { get; }

    /// <summary>
    /// Merged view of system + user events with load-event runners.
    /// </summary>
    public MergedEvents Events { get; }

    /// <summary>
    /// The goal currently being executed.
    /// </summary>
    public Goal? Goal { get; set; }

    /// <summary>
    /// The step currently being executed.
    /// </summary>
    public Step? Step { get; set; }

    public PLangContext(PLangAppContext appContext, MemoryStack? memoryStack = null, PLangContext? parent = null)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        AppContext = appContext;
        MemoryStack = memoryStack ?? new MemoryStack();
        Parent = parent;
        Depth = parent != null ? parent.Depth + 1 : 0;
        CreatedAt = DateTime.UtcNow;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(appContext.ShutdownToken);
        System = new EventScope();
        User = new EventScope();
        Events = new MergedEvents(System, User, appContext.Events);
    }

    /// <summary>
    /// Gets or sets a value in the execution context.
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
    /// Gets a typed value from the context.
    /// </summary>
    public T? Get<T>(string key)
    {
        if (_data.TryGetValue(key, out var value) && value is T typed)
            return typed;
        return default;
    }

    /// <summary>
    /// Sets a typed value in the context.
    /// </summary>
    public void Set<T>(string key, T value)
    {
        if (value == null)
            _data.TryRemove(key, out _);
        else
            _data[key] = value;
    }

    /// <summary>
    /// Checks if a key exists.
    /// </summary>
    public bool ContainsKey(string key) => _data.ContainsKey(key);

    /// <summary>
    /// Creates a child context for nested execution.
    /// </summary>
    public PLangContext CreateChild(MemoryStack? memoryStack = null)
    {
        return new PLangContext(AppContext, memoryStack ?? MemoryStack.Clone(), this);
    }

    /// <summary>
    /// Clones this context with a new memory stack.
    /// </summary>
    public PLangContext Clone(MemoryStack? memoryStack = null)
    {
        var clone = new PLangContext(AppContext, memoryStack ?? MemoryStack.Clone(), Parent)
        {
            IsAsync = IsAsync,
            CurrentGoalName = CurrentGoalName,
            CurrentStepIndex = CurrentStepIndex
        };

        foreach (var kvp in _data)
        {
            clone._data[kvp.Key] = kvp.Value;
        }

        return clone;
    }

    /// <summary>
    /// Requests cancellation of this execution.
    /// </summary>
    public void Cancel()
    {
        _cts?.Cancel();
    }

    /// <summary>
    /// Execution duration.
    /// </summary>
    public TimeSpan Duration => DateTime.UtcNow - CreatedAt;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

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

/// <summary>
/// Provides async-local access to PLangContext.
/// </summary>
public interface IPLangContextAccessor
{
    PLangContext? Current { get; set; }
}

/// <summary>
/// Default implementation using AsyncLocal.
/// </summary>
public class PLangContextAccessor : IPLangContextAccessor
{
    private static readonly AsyncLocal<PLangContext?> _current = new();

    public PLangContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
