namespace app.type.item.given;

/// <summary>
/// The decider's answer that a step gives an option it can't name a value of on its own — a permission, whose
/// <c>{path, verbs}</c> the writer fills from the step's words. Shown as <c>given</c>; on a formal line it writes
/// nothing, so the option enters as its bare name, a slot still to fill (<c>terminal.start(App, Permission)</c>).
/// </summary>
[global::app.Attributes.PlangType("given")]
public sealed class @this : global::app.type.item.@this
{
    /// <summary>The decider's machinery; no program names it.</summary>
    public static bool Internal => true;

    /// <summary>The one answer.</summary>
    public static readonly @this Instance = new();

    private @this() { }

    /// <summary>Its word everywhere but a formal line, where the option's bare name stands for it.</summary>
    public override void Write(global::app.type.format.IWriter w)
    {
        if (w.Format != global::app.goal.step.action.formal.Writer.Token) w.String("given");
    }

    public override string ToString() => "given";
}
