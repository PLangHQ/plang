namespace app.module;

/// <summary>
/// Capability interface for types that can carry event context.
/// The source generator detects parameters whose type implements IEvent.
/// If the resolved value has Event set, context.Event is set before Run().
/// </summary>
public interface IEvent
{
    /// <summary>
    /// The event context — contains the triggering step, phase, etc.
    /// Null when this is not an event-triggered call.
    /// </summary>
    EventContext? Event { get; set; }
}

/// <summary>
/// Event context carried on IEvent types. Accessible via %!event% in PLang.
/// </summary>
public class EventContext
{
    /// <summary>The step that triggered this event.</summary>
    public app.goal.steps.step.@this? Step { get; init; }

    /// <summary>The event phase (before/after).</summary>
    public EventPhase Phase { get; init; }
}
