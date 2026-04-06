using App.Context;
using App.Goals.Goal;

namespace App.modules;

/// <summary>
/// Events for a step/goal. Owns the logic to find matching bindings.
/// Context is injected so it can resolve from registered event bindings.
/// </summary>
public class Events : IContext
{
    private readonly object _owner;

    public Context.@this Context { get; set; } = null!;

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
        var actorContext = Context.Engine?.Context;
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
        if (_owner is App.Goals.Goal.Steps.Step.@this step)
        {
            var placeholder = new App.Goals.Goal.Steps.Step.Actions.Action.@this { Step = step };
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
