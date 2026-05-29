using app.actor.context;
using app.goal;

namespace app.modules;

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
    /// Resolves event bindings from the user context (where event.on registers them),
    /// regardless of which actor is currently executing.
    /// </summary>
    private List<GoalCall> GetBindings(EventPhase phase)
    {
        if (Context == null) return [];

        // Events are registered on the current actor's context
        var actorContext = Context.App?.CurrentActor.Context;
        if (actorContext != null)
        {
            var bindings = actorContext.GetEventBindings(_owner, phase);
            if (bindings.Count > 0) return bindings;
        }

        // Fallback to current context (for system-registered events)
        return Context.GetEventBindings(_owner, phase);
    }

    private List<GoalCall> Stamp(List<GoalCall> calls, EventPhase phase)
    {
        if (_owner is app.goal.steps.step.@this step)
        {
            var placeholder = new app.goal.steps.step.actions.action.@this { Step = step };
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
