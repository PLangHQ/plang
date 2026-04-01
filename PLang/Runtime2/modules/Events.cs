using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Goals.Goal;

namespace PLang.Runtime2.modules;

/// <summary>
/// Events for a step/goal. Owns the logic to find matching bindings.
/// Context is injected so it can resolve from registered event bindings.
/// </summary>
public class Events : IContext
{
    private readonly object _owner;

    public PLangContext Context { get; set; } = null!;

    public Events(object owner) => _owner = owner;

    public List<GoalCall> Before => Context?.GetEventBindings(_owner, EventPhase.Before) ?? [];
    public List<GoalCall> After => Context?.GetEventBindings(_owner, EventPhase.After) ?? [];
}

public enum EventPhase
{
    Before,
    After
}
