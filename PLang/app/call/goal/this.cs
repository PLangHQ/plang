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
        : base(null, type, stack.Current, stack.Current, stack, stack.Variables, names) => Goal = goal;

    /// <summary>A goal's frame come back from a snapshot: born with the goal its <paramref name="record"/>'s key finds,
    /// under <paramref name="stack"/>'s current frame.</summary>
    private @this(global::app.type.item.dict.@this record, global::app.type.@this type, list.@this stack, global::app.actor.context.@this context)
        : base(record, type, stack, context) => Goal = Kept<global::app.goal.@this>(record, "goal", context);

    /// <summary>The goal it runs.</summary>
    [Store, Out, Debug]
    public override global::app.goal.@this? Goal { get; }

    /// <summary>The pure core — a goal's frame passes through; nothing else converts into one.</summary>
    public static new @this? Create(object? raw) => raw as @this;

    /// <summary>A goal's frame passes through; a stored one's record comes back as one, made under the current frame of
    /// the stack <paramref name="data"/>'s context calls, not entered.</summary>
    public static new @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => raw switch
    {
        @this frame => frame,
        global::app.type.item.dict.@this record => new @this(record, type ?? data.Context.App.type.list[typeof(@this)], data.Context.call, data.Context),
        _ => null,
    };

    internal override bool IsGoal => true;
}
