namespace app.call.goal;

/// <summary>
/// A goal's frame: the goal starting, born with its call's names (its parameters). It is its run's scope
/// (<c>%!call.scope%</c>), and nothing of it is left once the goal's steps have run: its caller goes on.
/// </summary>
public sealed class @this : call.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>plang's own: no step names a frame's kind.</summary>
    public static bool Internal => true;

    internal @this(global::app.type.@this type, list.@this stack, global::app.goal.@this goal,
        IEnumerable<global::app.data.@this>? names)
        : base(null, type, stack.Current, stack.Current, stack, null, names) => Goal = goal;

    /// <summary>A goal's frame come back from a snapshot: born with the goal its <paramref name="record"/>'s key finds,
    /// under <paramref name="stack"/>'s current frame.</summary>
    private @this(global::app.type.item.dict.@this record, global::app.type.@this type, list.@this stack, global::app.actor.context.@this context)
        : base(record, type, stack, context) => Goal = Element<global::app.goal.@this>(record, "goal", context);

    /// <summary>The goal it runs.</summary>
    [Store, Out, Debug, global::app.Snapshot]
    public override global::app.goal.@this? Goal { get; }


    /// <summary>A goal's frame passes through; a stored one's record comes back as one, made under the current frame of
    /// the stack <paramref name="data"/>'s context calls, not entered.</summary>
    public static new @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => raw switch
    {
        @this frame => frame,
        global::app.type.item.dict.@this record => new @this(record, type ?? data.Context.App.type.list[typeof(@this)], data.Context.call, data.Context),
        _ => null,
    };

    internal override bool IsGoal => true;

    /// <summary>The answer as it leaves the goal: a return made in it ends here, and a return of more levels goes on, one
    /// fewer. A goal started and a goal's frame gone on from a snapshot leave the same way.</summary>
    internal override global::app.data.@this Leave(global::app.data.@this result)
    {
        if (!result.Returned) return result;
        result.ReturnDepth--;
        if (result.ReturnDepth <= 0) result.Returned = false;
        return result;
    }
}
