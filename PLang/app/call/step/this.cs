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
        : base(null, type, stack.Current, stack.Current, stack, null)
    {
        Step = step;
        index = step.Index;
    }

    /// <summary>A step's frame come back from a snapshot, under its caller (its goal's frame, or the action whose body it
    /// is in): born with the step at its saved <see cref="index"/> in that goal, as the goal is now. A place past the
    /// goal's end is refused, naming how many steps the goal holds, thrown to the read boundary.</summary>
    private @this(global::app.type.item.dict.@this record, global::app.type.@this type, list.@this stack, global::app.actor.context.@this context)
        : base(record, type, stack, context)
        => Step = Caller?.Goal is { } goal ? goal[index] ?? throw new global::app.error.DeclinedException(new global::app.error.@this(
                $"the saved step {index} is past the end of '{goal.Name}' ({goal.Step.Count} steps)", "StepNotFound", 404))
            : throw new global::app.error.DeclinedException(new global::app.error.@this(
                "a saved step's frame stands under no goal", "StepNotFound", 404));

    /// <summary>The step it runs; its goal is the step's.</summary>
    [Store, Out, Debug]
    public override global::app.goal.step.@this? Step { get; }

    /// <summary>Where the run stands in its goal: its step's place there — one of the two numbers that say where a resume
    /// goes on (the action's frame holds the other). Set in a snapshot, it moves where the run goes on.</summary>
    [global::app.Snapshot, global::app.LlmBuilder]
    public int index { get; set; }


    /// <summary>A step's frame passes through; a stored one's record comes back as one, made under the current frame of
    /// the stack <paramref name="data"/>'s context calls, not entered.</summary>
    public static new @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => raw switch
    {
        @this frame => frame,
        global::app.type.item.dict.@this record => new @this(record, type ?? data.Context.App.type.list[typeof(@this)], data.Context.call, data.Context),
        _ => null,
    };

    /// <summary>The rest of the list that holds its step, from the step after it — the step knows its list.</summary>
    internal override System.Threading.Tasks.Task<global::app.data.@this> Rest(global::app.actor.context.@this context, bool again,
        global::app.data.@this result)
        => Step!.List.Rest(Step, context, result);
}
