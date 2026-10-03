namespace app.goal.step.action.keep;

/// <summary>
/// A keep — <c>variable.set</c> (its handler is an <see cref="global::app.module.IKeep"/>): it keeps a value in a
/// variable. When the step's other actions produce a value (<c>set %x% = %x% + 1</c> → <c>math.add</c>), that
/// value is what it keeps, so it follows them with <c>Value=%!data%</c>; when none does (<c>set %x% = 5, write
/// out %x%</c>), it stays where it stands.
/// </summary>
[global::app.Attributes.PlangType("keep")]
public class @this : global::app.goal.step.action.@this
{
    /// <summary>A keep IS a distinct plang type (the role is the type): its wire shape is action's.</summary>
    protected internal override global::app.type.@this Type => new(typeof(@this));

    /// <summary>A keep holds its step's answer when it keeps what the actions before it produce (<c>Value=%!data%</c>,
    /// a <c>write to</c>); one with a value of its own (<c>set %seen% = %item%</c>) does not.</summary>
    internal override bool IsAnswer => Property["Value"]?.Value?.ToString() == Answer;

    // the Value a keep of its step's answer is written with, by Prefill and by the line
    private const string Answer = "%!data%";

    /// <summary>A program action of this keep's kind, in <paramref name="step"/>.</summary>
    internal override global::app.goal.step.action.@this Program(global::app.goal.step.@this? step)
        => new @this { Module = Module, Name = Name, Step = step, Synthetic = false };

    /// <summary>A keep takes its place once the line knows whether a value is produced: after the producers,
    /// keeping their value, or where it stands.</summary>
    internal override void Prefill(global::app.goal.step.pick.line.@this line, string call)
        => line.Keep(call, KeptValue.Replace(call, "Value=" + Answer, 1));

    // the starting line's Value slot — the name alone, before a comma or the closing parenthesis
    private static readonly System.Text.RegularExpressions.Regex KeptValue = new(@"\bValue(?=[,)])");
}
