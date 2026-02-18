namespace PLang.Runtime2.Engine.Events;

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
    OnCacheMiss
}
