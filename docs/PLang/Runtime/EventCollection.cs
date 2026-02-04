using System.Text.RegularExpressions;

namespace PLang.Runtime;

public partial class EventCollection
{
    private readonly List<EventEntry> _events = new();
    private readonly List<VariableChangeHandler> _variableChangingHandlers = new();
    private readonly List<VariableChangeHandler> _variableChangedHandlers = new();
    
    public EventEntry this[int index] => _events[index];
    public int Count => _events.Count;
    
    public void Add(EventType type, string? goalPathPattern, Func<Goal, Task> handler, bool isAsync = true)
    {
        _events.Add(new EventEntry(type, goalPathPattern, handler, isAsync));
    }
    
    public void AddBefore(string? goalPathPattern, Func<Goal, Task> handler, bool isAsync = true)
    {
        Add(EventType.Before, goalPathPattern, handler, isAsync);
    }
    
    public void AddAfter(string? goalPathPattern, Func<Goal, Task> handler, bool isAsync = true)
    {
        Add(EventType.After, goalPathPattern, handler, isAsync);
    }
    
    public IEnumerable<EventEntry> GetBefore(Goal goal)
        => _events.Where(e => e.Type == EventType.Before && e.Matches(goal));
    
    public IEnumerable<EventEntry> GetAfter(Goal goal)
        => _events.Where(e => e.Type == EventType.After && e.Matches(goal));
    
    public void Remove(EventEntry entry) => _events.Remove(entry);
    
    public void Clear() => _events.Clear();
    
    // Variable change events
    public void OnVariableChanging(Action<string, ObjectValue?, ObjectValue?> handler)
    {
        _variableChangingHandlers.Add(new VariableChangeHandler(handler));
    }
    
    public void OnVariableChanged(Action<string, ObjectValue?, ObjectValue?> handler)
    {
        _variableChangedHandlers.Add(new VariableChangeHandler(handler));
    }
    
    internal void OnVariableChanging(string key, ObjectValue? before, ObjectValue? after)
    {
        foreach (var handler in _variableChangingHandlers)
            handler.Invoke(key, before, after);
    }
    
    internal void OnVariableChanged(string key, ObjectValue? before, ObjectValue? after)
    {
        foreach (var handler in _variableChangedHandlers)
            handler.Invoke(key, before, after);
    }
}

public partial class VariableChangeHandler
{
    private readonly Action<string, ObjectValue?, ObjectValue?> _handler;
    
    public VariableChangeHandler(Action<string, ObjectValue?, ObjectValue?> handler)
    {
        _handler = handler;
    }
    
    public void Invoke(string key, ObjectValue? before, ObjectValue? after)
        => _handler(key, before, after);
}

public partial class EventEntry
{
    public EventType Type { get; }
    public string? GoalPathPattern { get; }
    public Func<Goal, Task> Handler { get; }
    public bool IsAsync { get; }
    
    public EventEntry(EventType type, string? goalPathPattern, Func<Goal, Task> handler, bool isAsync)
    {
        Type = type;
        GoalPathPattern = goalPathPattern;
        Handler = handler;
        IsAsync = isAsync;
    }
    
    public bool Matches(Goal goal)
    {
        if (GoalPathPattern == null) return true;
        return Regex.IsMatch(goal.Path, GoalPathPattern);
    }
}

public enum EventType
{
    Before,
    After
}
