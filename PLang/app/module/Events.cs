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

    /// <summary>The calls bound to run before this owner — resolved from this owner's context,
    /// where <c>event.on</c> registered them. One context per actor, so the owner's context IS its
    /// actor's context.</summary>
    public List<app.goal.step.action.@this> Before
        => Context == null ? [] : Context.GetEventBindings(_owner, EventPhase.Before);

    /// <summary>The calls bound to run after this owner.</summary>
    public List<app.goal.step.action.@this> After
        => Context == null ? [] : Context.GetEventBindings(_owner, EventPhase.After);
}

public enum EventPhase
{
    Before,
    After
}
