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

    /// <summary>An action's frame; <paramref name="variables"/> the store a diff captures (none: it captures none).</summary>
    internal @this(global::app.type.@this type, list.@this stack, global::app.goal.step.action.@this action,
        Variables? variables)
        : base(null, type, stack.Current, stack.Current, stack, variables) => Action = action;

    /// <summary>An action's frame come back from a snapshot: born with the action its <paramref name="record"/>'s key
    /// finds, under <paramref name="stack"/>'s current frame.</summary>
    private @this(global::app.type.item.dict.@this record, global::app.type.@this type, list.@this stack, global::app.actor.context.@this context)
        : base(record, type, stack, context) => Action = Kept<global::app.goal.step.action.@this>(record, "action", context);

    /// <summary>The action it runs; its step and goal are the action's.</summary>
    [Store, Out, Debug]
    public override global::app.goal.step.action.@this? Action { get; }


    /// <summary>An action's frame passes through; a stored one's record comes back as one, made under the current frame
    /// of the stack <paramref name="data"/>'s context calls, not entered.</summary>
    public static new @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => raw switch
    {
        @this frame => frame,
        global::app.type.item.dict.@this record => new @this(record, type ?? data.Context.App.type.list[typeof(@this)], data.Context.call, data.Context),
        _ => null,
    };

    /// <summary>The rest of its step's chain, as the chain runs it: this action again when the run stopped in it
    /// (<paramref name="again"/>: the ask takes its answer), else from the one after it (the call it made returned) —
    /// but a condition whose branch was running has taken it, which ends the chain.</summary>
    internal override System.Threading.Tasks.Task<global::app.data.@this> Rest(global::app.actor.context.@this context, bool again)
    {
        if (!again && Action!.IsCondition) return System.Threading.Tasks.Task.FromResult(context.Ok());
        // the action of an action's frame is one its step holds, so it has its place there
        var at = Action!.Index!.Value;
        return Step!.Code.Start(context, again ? at : at + 1);
    }
}
