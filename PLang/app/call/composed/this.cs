namespace app.call.composed;

/// <summary>
/// The frame of an action composed in C# (signing, a request another owner sends through http): it runs inside the
/// frame that composed it, and sits in no step's code, so nothing of it is left once it has run — its caller goes on.
/// </summary>
public sealed class @this : call.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>plang's own: no step names a frame's kind.</summary>
    public static bool Internal => true;

    /// <summary>A composed action's frame; <paramref name="variables"/> the store a diff captures (none: it captures none).</summary>
    internal @this(global::app.type.@this type, list.@this stack, global::app.goal.step.action.@this action,
        Variables? variables)
        : base(null, type, stack.Current, stack.Current, stack, variables) => Action = action;

    /// <summary>A composed action's frame come back from a snapshot, under <paramref name="stack"/>'s current frame: with
    /// no action, as it was kept.</summary>
    private @this(global::app.type.item.dict.@this record, global::app.type.@this type, list.@this stack, global::app.actor.context.@this context)
        : base(record, type, stack, context) { }

    /// <summary>The action it runs, which no step holds: in a trace, never kept, since nothing finds it again. A frame
    /// come back from a snapshot holds none; its rest was nothing either way.</summary>
    [Out, Debug]
    public override global::app.goal.step.action.@this? Action { get; }

    /// <summary>The pure core — a composed action's frame passes through; nothing else converts into one.</summary>
    public static new @this? Create(object? raw) => raw as @this;

    /// <summary>A composed action's frame passes through; a stored one's record comes back as one, made under the current
    /// frame of the stack <paramref name="data"/>'s context calls, not entered.</summary>
    public static new @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => raw switch
    {
        @this frame => frame,
        global::app.type.item.dict.@this record => new @this(record, type ?? data.Context.App.type.list[typeof(@this)], data.Context.call, data.Context),
        _ => null,
    };
}
