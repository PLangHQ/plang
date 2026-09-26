namespace app.goal.step.pick;

/// <summary>
/// One action the decider picked for a step: its name (<c>module.action</c>), its score (0 to 1) and
/// where the score came from — <c>stage 1</c> (a common action's own yes/no), <c>stage 2</c> (the main
/// module's action, scored by the module's share of the choice), <c>stage 2 yes/no</c> (a module asked
/// by name whether the step uses it), <c>branch</c> (an elseif/else asked by name after a picked if).
/// Build-time only: the decider's reading of the step, never stored in the .pr.
/// </summary>
public sealed class @this
{
    public string Name { get; init; } = "";

    /// <summary>The score, or the null citizen when the decider answered none.</summary>
    public global::app.type.item.number.@this? Score { get; init; }

    public string From { get; init; } = "";

    public override string ToString() => $"{Name} {Score} ({From})";
}
