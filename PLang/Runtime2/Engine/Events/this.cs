using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;

using EventBinding = PLang.Runtime2.Engine.Events.Lifecycle.Bindings.Binding.@this;

namespace PLang.Runtime2.Engine.Events;

/// <summary>
/// Manages event bindings and dispatching for Runtime2.
/// </summary>
public sealed class @this
{
    private readonly List<EventBinding> _bindings = new();
    private readonly object _lock = new();

    /// <summary>
    /// Called after any registration or unregistration to notify consumers (e.g., cache invalidation).
    /// </summary>
    public System.Action? OnChanged { get; set; }

    /// <summary>
    /// Registers an event binding.
    /// </summary>
    public string Register(EventBinding binding)
    {
        lock (_lock)
        {
            _bindings.Add(binding);
            _bindings.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
        OnChanged?.Invoke();
        return binding.Id;
    }

    /// <summary>
    /// Registers an event handler.
    /// </summary>
    public string Register(
        EventType type,
        Func<PLangContext, Task<Data>> handler,
        string? goalNamePattern = null,
        string? stepPattern = null,
        string? actionPattern = null,
        int priority = 0,
        bool stopOnError = true,
        bool isRegex = false)
    {
        return Register(new EventBinding(type, handler, goalNamePattern, stepPattern, actionPattern, priority, stopOnError, isRegex));
    }

    /// <summary>
    /// Unregisters an event binding by ID.
    /// </summary>
    public bool Unregister(string id)
    {
        bool removed;
        lock (_lock)
        {
            var index = _bindings.FindIndex(b => b.Id == id);
            if (index >= 0)
            {
                _bindings.RemoveAt(index);
                removed = true;
            }
            else
            {
                removed = false;
            }
        }
        if (removed) OnChanged?.Invoke();
        return removed;
    }

    /// <summary>
    /// Unregisters all bindings.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _bindings.Clear();
        }
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Gets all bindings of a specific type.
    /// </summary>
    public IReadOnlyList<EventBinding> GetBindings(EventType type)
    {
        lock (_lock)
        {
            return _bindings.Where(b => b.Type == type).ToList();
        }
    }

    /// <summary>
    /// Gets matching bindings for a goal/step/action.
    /// </summary>
    public IReadOnlyList<EventBinding> GetMatchingBindings(
        EventType type,
        string? goalName = null,
        string? stepText = null,
        string? module = null,
        string? actionName = null)
    {
        lock (_lock)
        {
            return _bindings
                .Where(b => b.Type == type)
                .Where(b => goalName == null || b.MatchesGoal(goalName))
                .Where(b => stepText == null || b.MatchesStep(stepText))
                .Where(b => module == null || actionName == null || b.MatchesAction(module, actionName))
                .ToList();
        }
    }

    /// <summary>
    /// Dispatches an event to all matching handlers.
    /// </summary>
    public async Task<Data> DispatchAsync(
        PLangContext context,
        EventType type,
        string? goalName = null,
        string? stepText = null,
        string? module = null,
        string? actionName = null)
    {
        var bindings = GetMatchingBindings(type, goalName, stepText, module, actionName);

        foreach (var binding in bindings)
        {
            var result = await binding.Handler(context);

            if (!result.Success && binding.StopOnError)
            {
                return result;
            }
        }

        return Data.Ok();
    }

    /// <summary>
    /// Gets the count of registered bindings.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _bindings.Count;
            }
        }
    }
}
