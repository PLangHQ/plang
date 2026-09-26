namespace app.goal.step.pick.question;

/// <summary>What a stage-2 question asks about.</summary>
public enum Kind
{
    /// <summary>Which one of the popular actions the step uses — a choice over <see cref="@this.Option"/>.</summary>
    Popular,
    /// <summary>Whether the step also has a branch of its if — a yes/no about <see cref="@this.Branch"/>.</summary>
    Branch,
    /// <summary>Whether the step uses a module — a yes/no about <see cref="@this.Module"/>.</summary>
    Use,
    /// <summary>Which action of a module the step calls — a choice over <see cref="@this.Option"/>.</summary>
    Action,
}

/// <summary>
/// One question stage 2 asks of a step: its id and what it is about — never its words. The step's
/// <c>Pick</c> decides WHICH questions are asked; the decider template writes them.
/// </summary>
public sealed class @this
{
    /// <summary>The question's id, <c>s{index}_{…}</c> — the answer comes back under it.</summary>
    public string Id { get; init; } = "";

    public Kind Kind { get; init; }

    /// <summary>The module asked about (<see cref="Kind.Use"/>, <see cref="Kind.Action"/>).</summary>
    public global::app.module.@this? Module { get; init; }

    /// <summary>A <see cref="Kind.Use"/> question about a module besides the main one: "also".</summary>
    public bool Also { get; init; }

    /// <summary>The branch asked about (<see cref="Kind.Branch"/>).</summary>
    public global::app.goal.step.action.@this? Branch { get; init; }

    /// <summary>The options of a choice (<see cref="Kind.Popular"/>, <see cref="Kind.Action"/>) — catalogue actions.</summary>
    public IReadOnlyList<global::app.goal.step.action.@this> Option { get; init; } = [];
}
