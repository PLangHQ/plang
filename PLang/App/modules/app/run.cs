using App.Errors;
using App.Variables;

namespace App.modules.app;

/// <summary>
/// Unified run action — runs a GoalCall, Step, or Action.
/// Actor switching is handled by the source generator.
/// </summary>
[ModuleDescription("Run a goal, step, or action on a specified actor")]
[System.ComponentModel.Description("Run a goal, step, or individual action, optionally switching to another actor")]
[Action("run")]
public partial class run : IContext
{
    public partial Data.@this<GoalCall>? GoalName { get; init; }
    public partial Data.@this<Step>? Step { get; init; }
    public partial Data.@this<Goals.Goal.Steps.Step.Actions.Action.@this>? Action { get; init; }
    public partial Data.@this<Actor.@this>? Actor { get; init; }

    public async Task<Data.@this> Run()
    {
        if (GoalName?.Value != null)
            return await Context.App!.RunGoalAsync(GoalName.Value, Context);

        if (Step?.Value != null)
            return await Step.Value.RunAsync(Context);

        if (Action?.Value != null)
            return await Action.Value.RunAsync(Context);

        return Error(new ActionError(
            "run requires a GoalCall, Step, or Action", "MissingInput", 400));
    }
}
