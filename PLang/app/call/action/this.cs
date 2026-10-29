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
        : base(null, type, stack.Current, stack.Current, stack, variables)
    {
        Action = action;
        Module = action.Module.Name;
        Name = action.Name;
    }

    /// <summary>An action's frame come back from a snapshot, under its step's frame: born with the action at its saved
    /// <see cref="index"/> in that step's code, as the goal is now. A place past the step's end is refused, naming how many
    /// actions the step holds, thrown to the read boundary.</summary>
    private @this(global::app.type.item.dict.@this record, global::app.type.@this type, list.@this stack, global::app.actor.context.@this context)
        : base(record, type, stack, context)
    {
        var step = Caller?.Step;
        Action = step != null && index >= 0 && index < step.Code.Count ? step.Code[index]
            : throw new global::app.error.DeclinedException(new global::app.error.@this(step != null
                ? $"the saved action {index} is past the end of step {step.Index} of '{step.Goal?.Name}' ({step.Code.Count} actions)"
                : "a saved action's frame stands under no step", "ActionNotFound", 404));
    }

    /// <summary>The action it runs; its step and goal are the action's.</summary>
    [Store, Out, Debug]
    public override global::app.goal.step.action.@this? Action { get; }

    /// <summary>Where the run stands in its step: its action's place in the step's code — one of the two numbers that say
    /// where a resume goes on (the step's frame holds the other). Set in a snapshot, it moves where the run goes on. A
    /// pushed frame finds it the first time it is asked (a snapshot written, a resume) and keeps it; a run that never asks
    /// never looks.</summary>
    [global::app.Snapshot, global::app.LlmBuilder]
    public int index { get => _index ??= Action?.Index ?? -1; set => _index = value; }
    private int? _index;

    /// <summary>The module of the action that stood there when it was saved — checked against the one there now.</summary>
    [global::app.Snapshot]
    internal string Module { get; private set; } = "";

    /// <summary>The name of the action that stood there when it was saved — checked against the one there now.</summary>
    [global::app.Snapshot]
    internal string Name { get; private set; } = "";


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
    internal override async System.Threading.Tasks.Task<global::app.data.@this> Rest(global::app.actor.context.@this context, bool again,
        global::app.data.@this result)
    {
        // a taken branch ends the chain: what it answered is the chain's
        if (!again && Action!.IsCondition) return result;
        // the action of an action's frame is one its step holds, at the frame's own place; after it, the chain goes on from
        // what it answered, as it would have
        var at = index;
        if (!again) return await Step!.Code.Start(context, at + 1, result);
        // the run goes on at its place as the goal is now: another action standing there (the goal changed, or the place
        // was edited) is said, and goes on
        if (!string.Equals(Action.Module.Name, Module, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Action.Name, Name, StringComparison.OrdinalIgnoreCase))
            await context.Actor.Channel[global::app.channel.list.@this.Error].WriteText(
                $"'{Step!.Goal?.Name}' step {Step.Index}, action {at} is {Action.Module.Name}.{Action.Name} now, not {Module}.{Name} " +
                "as it was saved: going on there");
        return await Step!.Code.Start(context, at);
    }
}
