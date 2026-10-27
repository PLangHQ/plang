namespace app.call.action;

/// <summary>
/// The frame of an action a step holds: its step's goal, its step and the action. Once the action has run, what is left
/// is the rest of its step's code: the action again where the run stopped in it, else from the one after it. (An action
/// composed in C# runs in a frame of its own kind, <see cref="composed.@this"/>.)
/// </summary>
public sealed class @this : call.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>plang's own: no step names a frame's kind.</summary>
    public static bool Internal => true;

    /// <summary>An action's frame; <paramref name="variables"/> the store a diff captures, else the stack's.</summary>
    internal @this(global::app.type.@this type, list.@this stack, global::app.goal.step.action.@this action,
        global::app.call.setting.@this setting, Variables? variables)
        : base(null, type, stack.Current, stack.Current, stack, variables ?? stack.Variables, setting) => Action = action;

    /// <summary>An action's frame come back from a snapshot: its stored <paramref name="node"/> read whole, under
    /// <paramref name="stack"/>'s current frame.</summary>
    private @this(global::app.snapshot.@this node, global::app.type.@this type, list.@this stack, global::app.actor.context.@this context)
        : base(node, type, stack, context) => Action = Placed(node, context).Action;

    /// <summary>The action it runs; its step and goal are the action's.</summary>
    [Store, Out, Debug]
    public override global::app.goal.step.action.@this? Action { get; }

    /// <summary>The pure core — an action's frame passes through; nothing else converts into one.</summary>
    public static new @this? Create(object? raw) => raw as @this;

    /// <summary>An action's frame passes through; a stored one's node comes back as one, under the current frame of the
    /// stack <paramref name="data"/>'s context calls.</summary>
    public static new @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => raw switch
    {
        @this frame => frame,
        global::app.snapshot.@this node => new @this(node, type ?? data.Context.App.type.list[typeof(@this)], data.Context.call, data.Context),
        _ => null,
    };

    /// <summary>The rest of its step's code: this action again when the run stopped in it (<paramref name="again"/>:
    /// the ask takes its answer), else from the one after it (the call it made returned).</summary>
    internal override System.Threading.Tasks.Task<global::app.data.@this> Rest(global::app.actor.context.@this context, bool again)
        => Step!.Resume(context, again ? Place : Place + 1);
}
