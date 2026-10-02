namespace app.call.binding;

/// <summary>
/// A frame that only binds names — a loop's <c>%item%</c>, a handler's <c>%error%</c>, a goal channel's
/// <c>%message%</c>, a validator's <c>%response%</c>, a tool call's state. It is a place in the chain (a failure
/// inside it happened there, so it stays in its caller's children), standing where its caller stands — its goal and
/// step are its caller's — but no goal, step or action of its own runs in it: it doesn't deepen the chain, it isn't
/// timed or diffed, and it is never a goal's own frame.
/// </summary>
public class @this : call.@this
{
    internal @this(call.@this? caller, list.@this stack, IEnumerable<global::app.data.@this>? names,
        global::app.goal.step.action.@this? held)
        : base(null, null, null, caller, stack, stack.Current, null, names, held) { }

    /// <summary>Its caller's goal — it stands where its caller stands.</summary>
    public override global::app.goal.@this? Goal => Caller?.Goal;

    /// <summary>Its caller's step.</summary>
    public override global::app.goal.step.@this? Step => Caller?.Step;

    internal override bool Deepens => false;

    internal override bool Measured => false;

    internal override bool IsGoal => false;
}
