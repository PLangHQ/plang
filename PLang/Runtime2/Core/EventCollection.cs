using PLang.Runtime2.Errors;

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
    OnVariableChange
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
    public Func<EventContext, Task<GoalResult>> Handler { get; }
    public int Priority { get; }
    public bool StopOnError { get; }

    public EventBinding(
        EventType type,
        Func<EventContext, Task<GoalResult>> handler,
        string? goalNamePattern = null,
        string? stepPattern = null,
        int priority = 0,
        bool stopOnError = true)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Type = type;
        GoalNamePattern = goalNamePattern;
        StepPattern = stepPattern;
        Handler = handler;
        Priority = priority;
        StopOnError = stopOnError;
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

        return stepText.Contains(StepPattern, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Context passed to event handlers.
/// </summary>
public sealed class EventContext
{
    public EventType EventType { get; init; }
    public string? GoalName { get; init; }
    public int? StepIndex { get; init; }
    public string? StepText { get; init; }
    public ErrorInfo? Error { get; init; }
    public object? Data { get; set; }
    public bool Cancel { get; set; }
}

/// <summary>
/// Manages event bindings and dispatching for Runtime2.
/// </summary>
public sealed class EventCollection
{
    private readonly List<EventBinding> _bindings = new();
    private readonly object _lock = new();

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
        return binding.Id;
    }

    /// <summary>
    /// Registers an event handler.
    /// </summary>
    public string Register(
        EventType type,
        Func<EventContext, Task<GoalResult>> handler,
        string? goalNamePattern = null,
        string? stepPattern = null,
        int priority = 0,
        bool stopOnError = true)
    {
        return Register(new EventBinding(type, handler, goalNamePattern, stepPattern, priority, stopOnError));
    }

    /// <summary>
    /// Unregisters an event binding by ID.
    /// </summary>
    public bool Unregister(string id)
    {
        lock (_lock)
        {
            var index = _bindings.FindIndex(b => b.Id == id);
            if (index >= 0)
            {
                _bindings.RemoveAt(index);
                return true;
            }
        }
        return false;
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
    /// Gets matching bindings for a goal/step.
    /// </summary>
    public IReadOnlyList<EventBinding> GetMatchingBindings(EventType type, string? goalName = null, string? stepText = null)
    {
        lock (_lock)
        {
            return _bindings
                .Where(b => b.Type == type)
                .Where(b => goalName == null || b.MatchesGoal(goalName))
                .Where(b => stepText == null || b.MatchesStep(stepText))
                .ToList();
        }
    }

    /// <summary>
    /// Dispatches an event to all matching handlers.
    /// </summary>
    public async Task<GoalResult> DispatchAsync(EventContext context)
    {
        var bindings = GetMatchingBindings(context.EventType, context.GoalName, context.StepText);

        foreach (var binding in bindings)
        {
            var result = await binding.Handler(context);

            if (!result.Success && binding.StopOnError)
            {
                return result;
            }

            if (context.Cancel)
            {
                return GoalResult.Ok();
            }
        }

        return GoalResult.Ok();
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
