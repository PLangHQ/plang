namespace app.call.step;

/// <summary>
/// A step's frame: its goal and the step. Once the step has run, what is left is the rest of the list that holds it —
/// its goal's steps, or the body of the action it sits in — from the step after it.
/// </summary>
public sealed class @this : call.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>plang's own: no step names a frame's kind.</summary>
    public static bool Internal => true;

    internal @this(global::app.type.@this type, list.@this stack, global::app.goal.step.@this step)
        : base(null, type, stack.Current, stack.Current, stack, null) => Step = step;

    /// <summary>A step's frame come back from a snapshot: born with the step its <paramref name="record"/>'s key finds,
    /// under <paramref name="stack"/>'s current frame.</summary>
    private @this(global::app.type.item.dict.@this record, global::app.type.@this type, list.@this stack, global::app.actor.context.@this context)
        : base(record, type, stack, context) => Step = Kept<global::app.goal.step.@this>(record, "step", context);

    /// <summary>The step it runs; its goal is the step's.</summary>
    [Store, Out, Debug]
    public override global::app.goal.step.@this? Step { get; }


    /// <summary>A step's frame passes through; a stored one's record comes back as one, made under the current frame of
    /// the stack <paramref name="data"/>'s context calls, not entered.</summary>
    public static new @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => raw switch
    {
        @this frame => frame,
        global::app.type.item.dict.@this record => new @this(record, type ?? data.Context.App.type.list[typeof(@this)], data.Context.call, data.Context),
        _ => null,
    };

    /// <summary>The rest of the list that holds its step, from the step after it — the step knows its list.</summary>
    internal override System.Threading.Tasks.Task<global::app.data.@this> Rest(global::app.actor.context.@this context, bool again)
        => Step!.List.Rest(Step, context);
}
