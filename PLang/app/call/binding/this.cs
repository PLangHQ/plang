namespace app.call.binding;

/// <summary>
/// A frame that only binds names — a loop's <c>%item%</c>, a handler's <c>%error%</c>, a goal channel's
/// <c>%message%</c>, a validator's <c>%response%</c>, a tool call's state. It is a place in the chain (a failure
/// inside it happened there, so it stays in its caller's children), standing where its caller stands — its goal and
/// step are its caller's — but no goal, step or action of its own runs in it: it doesn't deepen the chain, it isn't
/// timed or diffed, and it is never a goal's own frame.
/// </summary>
public class @this : call.@this, global::app.type.item.ICreate<@this>
{
    internal @this(global::app.type.@this type, global::app.call.@this? caller, list.@this stack,
        IEnumerable<global::app.data.@this>? names, global::app.goal.step.action.@this? call)
        : base(null, type, caller, stack.Current, stack, null, names, call) { }

    /// <summary>A binding come back from a snapshot: its stored <paramref name="record"/> read onto it, under
    /// <paramref name="stack"/>'s current frame. The call it was pushed for is not stored: it was read only while that
    /// call bound its parameters, before anything ran under the binding.</summary>
    private protected @this(global::app.type.item.dict.@this record, global::app.type.@this type, list.@this stack,
        global::app.actor.context.@this context)
        : base(record, type, stack, context) { }


    /// <summary>A binding passes through; a stored binding's record comes back as one, made under the current frame of
    /// the stack <paramref name="data"/>'s context calls, not entered.</summary>
    public static new @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => raw switch
    {
        @this binding => binding,
        global::app.type.item.dict.@this record => new @this(record, type ?? data.Context.App.type.list[typeof(@this)], data.Context.call, data.Context),
        _ => null,
    };

    /// <summary>Its caller's goal — it stands where its caller stands.</summary>
    public override global::app.goal.@this? Goal => Caller?.Goal;

    /// <summary>Its caller's step.</summary>
    public override global::app.goal.step.@this? Step => Caller?.Step;

    internal override bool Deepens => false;

    internal override bool Measured => false;
}
