using app.error;
using app.variables;

namespace app.modules.environment;

/// <summary>
/// Unified run action — runs a GoalCall, Step, or Action.
/// Actor switching is handled by the source generator.
/// </summary>
[Action("run")]
public partial class run : IContext
{
    public partial data.@this<GoalCall>? GoalName { get; init; }
    public partial data.@this<Step>? Step { get; init; }
    public partial data.@this<goals.goal.steps.step.actions.action.@this>? Action { get; init; }
    public partial data.@this<actor.@this>? Actor { get; init; }

    public async Task<data.@this> Run()
    {
        // Polymorphic: forwarded result type depends on the dispatched target.
        if (GoalName?.Value != null)
            return await Context.App!.RunGoalAsync(GoalName.Value, Context);

        if (Step?.Value != null)
            return await Step.Value.RunAsync(Context);

        if (Action?.Value != null)
            return await Action.Value.RunAsync(Context);

        return data.@this<object>.FromError(new ActionError(
            "run requires a GoalCall, Step, or Action", "MissingInput", 400));
    }
}
