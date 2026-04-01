using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Goals.Goal;

namespace PLang.Runtime2.modules;

/// <summary>
/// Event resolver — returns matching event bindings for the owner (Step, Goal, etc.).
/// Implements IContext so the MemoryStack can inject context during dot-path traversal.
/// The owner's type determines which bindings match (step-level, goal-level, etc.).
/// </summary>
public class Event : IContext
{
    private readonly object _owner;

    public PLangContext Context { get; set; } = null!;

    public Event(object owner) => _owner = owner;

    public List<GoalCall> Before => Context?.GetEventBindings(_owner, EventPhase.Before) ?? [];
    public List<GoalCall> After => Context?.GetEventBindings(_owner, EventPhase.After) ?? [];
}

public enum EventPhase
{
    Before,
    After
}
