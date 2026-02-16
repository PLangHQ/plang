using System.Text.RegularExpressions;
using PLang.Runtime2.Context;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Core;

/// <summary>
/// Types of events in the PLang runtime lifecycle.
/// </summary>
public enum EventType
{
    BeforeAppStart,
    AfterAppStart,
    BeforeGoal,
    AfterGoal,
    BeforeStep,
    AfterStep,
    OnError,
    OnVariableChange,
    OnBeforeGoalLoad,
    OnAfterGoalLoad,
    OnBeforeStepLoad,
    OnAfterStepLoad,
    BeforeAction,
    AfterAction,
    OnCacheHit,
    OnCacheMiss
}

/// <summary>
/// Represents an event binding in Runtime2.
/// </summary>
public sealed class EventBinding
{
    public string Id { get; }
    public EventType Type { get; }
    public string? GoalNamePattern { get; }
    public string? StepPattern { get; }
    public string? ActionPattern { get; }
    public Func<PLangContext, Task<Data>> Handler { get; }
    public int Priority { get; }
    public bool StopOnError { get; }
    public bool IsRegex { get; }

    public List<object> Targets { get; } = new();

    /// <summary>
    /// Runs this binding's handler, skipping if already executing (re-entry guard).
    /// </summary>
    public async Task<Data> Run(PLangContext context)
    {
        if (!context.TryEnterEvent(Id))
            return Data.Ok();

        var result = await Handler(context);
        context.ExitEvent(Id);

        // Check if handler set an override via event.skipAction.
        // Only consume the override for action-level events (BeforeAction/AfterAction)
        // to prevent step/goal-level events from accidentally eating the override.
        if (Type == EventType.BeforeAction || Type == EventType.AfterAction)
        {
            var @override = context.EventOverride;
            if (@override != null)
            {
                context.EventOverride = null;
                @override.Handled = true;
                return @override;
            }
        }

        if (!result.Success && !StopOnError)
            return Data.Ok();

        return result;
    }

    public EventBinding(
        EventType type,
        Func<PLangContext, Task<Data>> handler,
        string? goalNamePattern = null,
        string? stepPattern = null,
        string? actionPattern = null,
        int priority = 0,
        bool stopOnError = true,
        bool isRegex = false)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Type = type;
        GoalNamePattern = goalNamePattern;
        StepPattern = stepPattern;
        ActionPattern = actionPattern;
        Handler = handler;
        Priority = priority;
        StopOnError = stopOnError;
        IsRegex = isRegex;
    }

    /// <summary>
    /// Checks if this binding matches the given goal name.
    /// </summary>
    public bool MatchesGoal(string goalName)
    {
        if (string.IsNullOrEmpty(GoalNamePattern))
            return true;

        if (GoalNamePattern == "*")
            return true;

        if (IsRegex)
            return Regex.IsMatch(goalName, GoalNamePattern, RegexOptions.IgnoreCase);

        if (GoalNamePattern.EndsWith("*"))
        {
            var prefix = GoalNamePattern[..^1];
            return goalName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(goalName, GoalNamePattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if this binding matches the given step text.
    /// </summary>
    public bool MatchesStep(string stepText)
    {
        if (string.IsNullOrEmpty(StepPattern))
            return true;

        if (StepPattern == "*")
            return true;

        if (IsRegex)
            return Regex.IsMatch(stepText, StepPattern, RegexOptions.IgnoreCase);

        return stepText.Contains(StepPattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if this binding matches the given module and action name.
    /// Supports exact match ("variable.set") and wildcard ("variable.*").
    /// </summary>
    public bool MatchesAction(string module, string actionName)
    {
        if (string.IsNullOrEmpty(ActionPattern))
            return true;

        if (ActionPattern == "*")
            return true;

        var fullName = $"{module}.{actionName}";

        if (IsRegex)
            return Regex.IsMatch(fullName, ActionPattern, RegexOptions.IgnoreCase);

        if (ActionPattern.EndsWith(".*"))
        {
            var prefix = ActionPattern[..^2];
            return module.Equals(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(fullName, ActionPattern, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Manages event bindings and dispatching for Runtime2.
/// </summary>
public sealed class Events
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
