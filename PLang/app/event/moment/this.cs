namespace app.@event.moment;

/// <summary>
/// What happened when a lifecycle event fired: the trigger and the ONE node it fired on — a goal, a
/// step, or an action (with its result for AfterAction). The rest is reached by navigation, never
/// copied beside it: an action's step is <c>action.Step</c>, a step's goal is <c>step.Goal</c>.
/// The firing node builds it; the binding sets it as <c>%!event%</c> before its handler runs, so a
/// called goal reads <c>%!event.step.Text%</c> or <c>%!event.goal.Name%</c>.
/// </summary>
public sealed class @this : global::app.type.item.@this
{
    private readonly global::app.goal.@this? _goal;
    private readonly global::app.goal.step.@this? _step;

    public @this(Trigger trigger, global::app.goal.@this goal) { Trigger = trigger; _goal = goal; }

    public @this(Trigger trigger, global::app.goal.step.@this step) { Trigger = trigger; _step = step; }

    public @this(Trigger trigger, global::app.goal.step.action.@this action, global::app.data.@this? result = null)
    {
        Trigger = trigger;
        Action = action;
        Result = result;
    }

    /// <summary>The lifecycle moment that fired.</summary>
    public Trigger Trigger { get; }

    /// <summary>The action it fired on — null for goal and step events.</summary>
    public global::app.goal.step.action.@this? Action { get; }

    /// <summary>The action's result — set for AfterAction only.</summary>
    public global::app.data.@this? Result { get; }

    /// <summary>The step it fired on, or the step of the action it fired on.</summary>
    public global::app.goal.step.@this? Step => _step ?? Action?.Step;

    /// <summary>The goal it fired on, or the goal of its step.</summary>
    public global::app.goal.@this? Goal => _goal ?? Step?.Goal;
}
