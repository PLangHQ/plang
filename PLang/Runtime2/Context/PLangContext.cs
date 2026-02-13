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
    /// The goal currently being executed.
    /// </summary>
    public Goal? Goal { get; set; }

    /// <summary>
    /// The step currently being executed.
    /// </summary>
    public Step? Step { get; set; }

    /// <summary>
    /// Set by event.skipAction to override the current action's result.
    /// Cleared by EventBinding.Run after reading.
    /// </summary>
    public Data? EventOverride { get; set; }

    /// <summary>
    /// Direct reference to the Engine. Set by RegisterContextVariables().
    /// </summary>
    public Engine? Engine { get; private set; }

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

        // Wire event registration to invalidate the resolved-events cache
        System.Events.OnChanged = InvalidateEventCache;
        User.Events.OnChanged = InvalidateEventCache;
    }

    /// <summary>
    /// Registers context variables (prefixed with !) on the memory stack.
    /// Called after Engine is available.
    /// </summary>
    public void RegisterContextVariables(Engine engine)
    {
        Engine = engine;
        var ms = MemoryStack;

        // Static references (same object for lifetime of context)
        ms.Put(new Data("!engine", engine));
        ms.Put(new Data("!context", this));
        ms.Put(new Data("!memoryStack", ms));
        ms.Put(new Data("!fileSystem", engine.FileSystem));
        ms.Put(new Data("!callStack", CallStack));
        ms.Put(new Data("!io", engine.IO));
        ms.Put(new Data("!serializers", engine.Serializers));

        // Dynamic references (change per goal/step)
        ms.Put(new DynamicData("!goal", () => Goal));
        ms.Put(new DynamicData("!step", () => Step));
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

    private readonly ConcurrentDictionary<object, object> _eventContainers = new();
    private readonly ConcurrentDictionary<string, byte> _activeEventBindings = new();

    /// <summary>
    /// Returns true if the event binding is not already running, and marks it as active.
    /// Used to prevent re-entrant event handler execution.
    /// </summary>
    internal bool TryEnterEvent(string bindingId) => _activeEventBindings.TryAdd(bindingId, 0);

    /// <summary>
    /// Marks an event binding as no longer active.
    /// </summary>
    internal void ExitEvent(string bindingId) => _activeEventBindings.TryRemove(bindingId, out _);

    /// <summary>
    /// Clears the event resolution cache. Must be called when events are registered
    /// during execution so newly added events are picked up on subsequent EventsFor() calls.
    /// </summary>
    public void InvalidateEventCache() => _eventContainers.Clear();

    /// <summary>
    /// Resolves per-context events for a Goal. Lazy-resolves from User.Events on first call, cached on context.
    /// </summary>
    public Core.GoalStepEvents EventsFor(Core.Goal goal)
    {
        return (Core.GoalStepEvents)_eventContainers.GetOrAdd(goal, _ =>
        {
            var events = new Core.GoalStepEvents();
            var userEvents = User.Events;

            foreach (var b in userEvents.GetMatchingBindings(Core.EventType.OnBeforeGoalLoad, goalName: goal.Name))
                events.Load.Before.Add(b);
            foreach (var b in userEvents.GetMatchingBindings(Core.EventType.OnAfterGoalLoad, goalName: goal.Name))
                events.Load.After.Add(b);
            foreach (var b in userEvents.GetMatchingBindings(Core.EventType.BeforeGoal, goalName: goal.Name))
                events.Before.Add(b);
            foreach (var b in userEvents.GetMatchingBindings(Core.EventType.AfterGoal, goalName: goal.Name))
                events.After.Add(b);

            return events;
        });
    }

    /// <summary>
    /// Resolves per-context events for a Step. Lazy-resolves from User.Events on first call, cached on context.
    /// </summary>
    public Core.GoalStepEvents EventsFor(Core.Step step)
    {
        return (Core.GoalStepEvents)_eventContainers.GetOrAdd(step, _ =>
        {
            var events = new Core.GoalStepEvents();
            var userEvents = User.Events;
            var goalName = step.Goal?.Name;

            foreach (var b in userEvents.GetMatchingBindings(Core.EventType.OnBeforeStepLoad, goalName: goalName, stepText: step.Text))
                events.Load.Before.Add(b);
            foreach (var b in userEvents.GetMatchingBindings(Core.EventType.OnAfterStepLoad, goalName: goalName, stepText: step.Text))
                events.Load.After.Add(b);
            foreach (var b in userEvents.GetMatchingBindings(Core.EventType.BeforeStep, goalName: goalName, stepText: step.Text))
                events.Before.Add(b);
            foreach (var b in userEvents.GetMatchingBindings(Core.EventType.AfterStep, goalName: goalName, stepText: step.Text))
                events.After.Add(b);

            if (step.StepCache != null)
            {
                foreach (var b in userEvents.GetMatchingBindings(Core.EventType.OnCacheHit, goalName: goalName, stepText: step.Text))
                    step.StepCache.Hit.Add(b);
                foreach (var b in userEvents.GetMatchingBindings(Core.EventType.OnCacheMiss, goalName: goalName, stepText: step.Text))
                    step.StepCache.Miss.Add(b);
            }

            return events;
        });
    }

    /// <summary>
    /// Resolves per-context events for an Action. Lazy-resolves from User.Events on first call, cached on context.
    /// </summary>
    public Core.ActionEvents EventsFor(Core.Action action)
    {
        return (Core.ActionEvents)_eventContainers.GetOrAdd(action, _ =>
        {
            var events = new Core.ActionEvents();
            var userEvents = User.Events;

            foreach (var b in userEvents.GetMatchingBindings(Core.EventType.BeforeAction, module: action.Module, actionName: action.ActionName))
                events.Before.Add(b);
            foreach (var b in userEvents.GetMatchingBindings(Core.EventType.AfterAction, module: action.Module, actionName: action.ActionName))
                events.After.Add(b);

            return events;
        });
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
