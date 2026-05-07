namespace App.Events;

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
    // Stage 8 — channel lifecycle. Bindings filter by ChannelName.
    BeforeWrite,
    AfterWrite,
    BeforeRead,
    AfterRead,
    OnAsk
}
