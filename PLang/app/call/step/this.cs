namespace app.call.step;

/// <summary>
/// A step's frame: its goal and the step. Once the step has run, what is left is the rest of the list that holds it —
/// its goal's steps, or the body of the action it sits in — from the step after it.
/// </summary>
public sealed class @this : call.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>plang's own: no step names a frame's kind.</summary>
    public static bool Internal => true;

    internal @this(global::app.type.@this type, list.@this stack, global::app.goal.step.@this step,
        global::app.call.setting.@this setting)
        : base(null, type, stack.Current, stack.Current, stack, stack.Variables, setting) => Step = step;

    /// <summary>A step's frame come back from a snapshot: its stored <paramref name="node"/> read whole, under
    /// <paramref name="stack"/>'s current frame.</summary>
    private @this(global::app.snapshot.@this node, global::app.type.@this type, list.@this stack, global::app.actor.context.@this context)
        : base(node, type, stack, context) => Step = Placed(node, context).Step;

    /// <summary>The step it runs; its goal is the step's.</summary>
    [Store, Out, Debug]
    public override global::app.goal.step.@this? Step { get; }

    /// <summary>The pure core — a step's frame passes through; nothing else converts into one.</summary>
    public static new @this? Create(object? raw) => raw as @this;

    /// <summary>A step's frame passes through; a stored one's node comes back as one, under the current frame of the
    /// stack <paramref name="data"/>'s context calls.</summary>
    public static new @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => raw switch
    {
        @this frame => frame,
        global::app.snapshot.@this node => new @this(node, type ?? data.Context.App.type.list[typeof(@this)], data.Context.call, data.Context),
        _ => null,
    };

    /// <summary>The rest of the list that holds its step, from the step after it — the step knows its list.</summary>
    internal override System.Threading.Tasks.Task<global::app.data.@this> Rest(global::app.actor.context.@this context, bool again)
        => Step!.List.Rest(Step, context);
}
