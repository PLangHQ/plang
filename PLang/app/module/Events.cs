using app.actor.context;
using app.goal;

namespace app.module;

/// <summary>
/// Events for a step/goal. Owns the logic to find matching bindings.
/// Context is injected so it can resolve from registered event bindings.
/// </summary>
public class Events : IContext
{
    private readonly object _owner;

    public actor.context.@this Context { get; set; } = null!;

    public Events(object owner) => _owner = owner;

    public List<GoalCall> Before => Stamp(GetBindings(EventPhase.Before), EventPhase.Before);
    public List<GoalCall> After => Stamp(GetBindings(EventPhase.After), EventPhase.After);

    /// <summary>
    /// Resolves event bindings from this owner's context — where <c>event.on</c> registered
    /// them. One context per actor, so the owner's context IS its actor's context.
    /// </summary>
    private List<GoalCall> GetBindings(EventPhase phase)
    {
        if (Context == null) return [];
        return Context.GetEventBindings(_owner, phase);
    }

    private List<GoalCall> Stamp(List<GoalCall> calls, EventPhase phase)
    {
        if (_owner is app.goal.step.@this step)
        {
            var placeholder = new app.goal.step.action.@this { Step = step };
            foreach (var gc in calls)
            {
                gc.Action = placeholder;
                gc.Event = new EventContext { Step = step, Phase = phase };
            }
        }
        return calls;
    }
}

public enum EventPhase
{
    Before,
    After
}
