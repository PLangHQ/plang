namespace app.goal.step.pick.listed;

/// <summary>How sure the decider is of a listed action — the prompt marks each one its own way.</summary>
public enum Mark
{
    /// <summary>0.90 or more: in the step's line for certain.</summary>
    Certain,
    /// <summary>Below 0.90: used only when the step's words need it.</summary>
    Possible,
    /// <summary>The variable.set of a <c>write to %x%</c> — a known value, not a guess.</summary>
    WriteTo,
    /// <summary>From the popular-action choice on a step the decider was unsure of.</summary>
    Popular,
}

/// <summary>One action the prompt lists for a step: its name, its score and its mark.</summary>
public sealed class @this
{
    public string Name { get; init; } = "";

    public global::app.type.item.number.@this Score { get; init; } = 0;

    /// <summary>The score as the prompt prints it: two decimals (<c>0.99</c>, <c>1.00</c>).</summary>
    public string Shown => ((double)Score).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    public Mark Mark { get; init; }

    /// <summary>For an action picked through its module: how sure stage 1 was of each module stage 2 asked about,
    /// as the prompt prints it (<c>file 0.56, variable 0.36</c>) — the action's score is its certainty within its
    /// module, not the module's. Null for any other action.</summary>
    public string? Module { get; init; }

    /// <summary>The options the decider chose a value of for this action (<c>Template=plang</c>), as the starting line
    /// writes them — carried whether the action is certain or not, so an uncertain one keeps them too.</summary>
    public IReadOnlyList<string> Option { get; init; } = [];
}
