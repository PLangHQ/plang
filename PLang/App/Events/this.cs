using EventBinding = App.Events.Lifecycle.Bindings.Binding.@this;

namespace App.Events;

/// <summary>
/// Manages event bindings and dispatching for App.
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
        }
        OnChanged?.Invoke();
        return binding.Id;
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
    /// Saves a snapshot of current bindings for later restore.
    /// </summary>
    public List<EventBinding> Save()
    {
        lock (_lock) { return new List<EventBinding>(_bindings); }
    }

    /// <summary>
    /// Restores bindings from a saved snapshot.
    /// </summary>
    public void Restore(List<EventBinding> snapshot)
    {
        lock (_lock)
        {
            _bindings.Clear();
            _bindings.AddRange(snapshot);
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
