using App.Errors;
using App.Variables;

namespace App.modules.app;

/// <summary>
/// Unified run action — runs a GoalCall, Step, or Action.
/// Actor switching is handled by the source generator.
/// </summary>
[Action("run")]
public partial class run : IContext
{
    public partial GoalCall? GoalName { get; init; }
    public partial Step? Step { get; init; }
    public partial Goals.Goal.Steps.Step.Actions.Action.@this? Action { get; init; }
    public partial Actor.@this? Actor { get; init; }

    public async Task<Data.@this> Run()
    {
        if (GoalName != null)
        {
            var app = Context.App!;
            var goalResult = await GoalName.GetGoalAsync(app, Context);
            if (!goalResult.Success) return goalResult;

            return await app.RunGoalAsync((Goals.Goal.@this)goalResult.Value!, Context, Context.CancellationToken);
        }

        if (Step != null)
            return await Step.RunAsync();

        if (Action != null)
            return await Action.RunAsync(Context);

        return Error(new ActionError(
            "run requires a GoalCall, Step, or Action", "MissingInput", 400));
    }
}
