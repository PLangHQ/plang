namespace app.events;

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
    OnVariableChange,
    OnBeforeGoalLoad,
    OnAfterGoalLoad,
    OnBeforeStepLoad,
    OnAfterStepLoad,
    BeforeAction,
    AfterAction,
    OnCacheHit,
    OnCacheMiss,
    // Channel lifecycle. Bindings filter by ChannelName.
    BeforeWrite,
    AfterWrite,
    BeforeRead,
    AfterRead,
    OnAsk
}
