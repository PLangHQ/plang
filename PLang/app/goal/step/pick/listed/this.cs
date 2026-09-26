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
}
