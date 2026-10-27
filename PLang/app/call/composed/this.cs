namespace app.call.composed;

/// <summary>
/// The frame of an action composed in C# (signing, a request another owner sends through http): it runs inside the
/// frame that composed it, and sits in no step's code, so nothing of it is left once it has run — its caller goes on.
/// </summary>
public sealed class @this : call.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>plang's own: no step names a frame's kind.</summary>
    public static bool Internal => true;

    /// <summary>A composed action's frame; <paramref name="variables"/> the store a diff captures, else the stack's.</summary>
    internal @this(global::app.type.@this type, list.@this stack, global::app.goal.step.action.@this action,
        Variables? variables)
        : base(null, type, stack.Current, stack.Current, stack, variables ?? stack.Variables) => Action = action;

    /// <summary>A composed action's frame come back from a snapshot: its stored <paramref name="node"/> read whole,
    /// under <paramref name="stack"/>'s current frame.</summary>
    private @this(global::app.snapshot.@this node, global::app.type.@this type, list.@this stack, global::app.actor.context.@this context)
        : base(node, type, stack, context) => Action = Placed(node, context).Action;

    /// <summary>The action it runs, which no step holds: in a trace, never kept, since nothing finds it again. A frame
    /// come back from a snapshot holds none; its rest was nothing either way.</summary>
    [Out, Debug]
    public override global::app.goal.step.action.@this? Action { get; }

    /// <summary>The pure core — a composed action's frame passes through; nothing else converts into one.</summary>
    public static new @this? Create(object? raw) => raw as @this;

    /// <summary>A composed action's frame passes through; a stored one's node comes back as one, under the current
    /// frame of the stack <paramref name="data"/>'s context calls.</summary>
    public static new @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => raw switch
    {
        @this frame => frame,
        global::app.snapshot.@this node => new @this(node, type ?? data.Context.App.type.list[typeof(@this)], data.Context.call, data.Context),
        _ => null,
    };
}
