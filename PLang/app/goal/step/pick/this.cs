namespace app.goal.step.pick;

/// <summary>Where a pick's score came from.</summary>
public enum From
{
    /// <summary>Stage 1: a common action's own yes/no.</summary>
    Common,
    /// <summary>Stage 2: the main module's action, scored by the module's share of stage 1's choice.</summary>
    Choice,
    /// <summary>Stage 2: a module asked by name whether the step uses it, scored by that yes/no.</summary>
    YesNo,
    /// <summary>Stage 2: an elseif/else asked by name after a picked if.</summary>
    Branch,
}

/// <summary>
/// One action the decider picked for a step: its name (<c>module.action</c>), its score (0 to 1) and
/// where the score came from. Build-time only: the decider's reading of the step, never stored in the .pr.
/// </summary>
public sealed class @this
{
    public string Name { get; init; } = "";

    /// <summary>The score, or null when the decider answered none.</summary>
    public global::app.type.item.number.@this? Score { get; init; }

    public From From { get; init; }

    public override string ToString() => $"{Name} {Score} ({From})";
}
