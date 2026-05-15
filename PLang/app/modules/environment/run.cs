using app.Errors;
using app.Variables;

namespace app.modules.environment;

/// <summary>
/// Unified run action — runs a GoalCall, Step, or Action.
/// Actor switching is handled by the source generator.
/// </summary>
[ModuleDescription("Run a goal, step, or action on a specified actor")]
[System.ComponentModel.Description("Run a goal, step, or individual action, optionally switching to another actor")]
[Action("run")]
public partial class run : IContext
{
    public partial data.@this<GoalCall>? GoalName { get; init; }
    public partial data.@this<Step>? Step { get; init; }
    public partial data.@this<Goals.Goal.Steps.Step.Actions.Action.@this>? Action { get; init; }
    public partial data.@this<Actor.@this>? Actor { get; init; }

    public async Task<data.@this> Run()
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
