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

    public List<GoalCall> Before => Stamp(Context?.GetEventBindings(_owner, EventPhase.Before) ?? []);
    public List<GoalCall> After => Stamp(Context?.GetEventBindings(_owner, EventPhase.After) ?? []);

    private List<GoalCall> Stamp(List<GoalCall> calls)
    {
        // For events, we don't have an action — create a placeholder action with the step
        if (_owner is PLang.Runtime2.Engine.Goals.Goal.Steps.Step.@this step)
        {
            var placeholder = new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this { Step = step };
            foreach (var gc in calls) gc.Action = placeholder;
        }
        return calls;
    }
}

public enum EventPhase
{
    Before,
    After
}
