namespace app.goal.step.action.loop;

/// <summary>
/// A loop — <c>loop.foreach</c> (its handler is an <see cref="global::app.module.ILoop"/>): it runs the actions
/// after it in its step for each item, so it leads the step's code. It answers that in the code the build knows
/// before the LLM does (<see cref="Know"/>) and in the formal line pick pre-fills (<see cref="Prefill"/>).
/// </summary>
[global::app.Attributes.PlangType("loop")]
public class @this : global::app.goal.step.action.@this
{
    /// <summary>A loop IS a distinct plang type (the role is the type): its wire shape is action's.</summary>
    protected internal override global::app.type.@this Type => new(typeof(@this));

    /// <summary>A program action of this loop's kind, in <paramref name="step"/>.</summary>
    internal override global::app.goal.step.action.@this Program(global::app.goal.step.@this? step)
        => new @this { Module = Module, Name = Name, Step = step, Synthetic = false };

    /// <summary>A loop leads the pre-filled line: what follows it is what it runs.</summary>
    internal override void Prefill(global::app.goal.step.pick.line.@this line, string call) => line.Lead(call);

    /// <summary>A loop leads the known code.</summary>
    internal override void Know(List<string> line, string call) => line.Insert(0, call);
}
