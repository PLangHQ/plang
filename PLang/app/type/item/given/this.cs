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

    /// <summary>It is a slot still to fill; the writer says how one is written.</summary>
    public override void Write(global::app.type.format.IWriter w) => w.Given();

    public override string ToString() => "given";
}
