using App.Errors;
using App.Variables;

namespace App.modules.app;

/// <summary>
/// Unified run action — runs a GoalCall, Step, or Action.
/// Each object owns its own execution behavior.
/// </summary>
[Action("run")]
public partial class run : IContext
{
    public partial GoalCall? GoalName { get; init; }
    public partial Step? Step { get; init; }
    public partial Goals.Goal.Steps.Step.Actions.Action.@this? Action { get; init; }
    public partial Context.Actor? Actor { get; init; }

    public async Task<Data.@this> Run()
    {
        var app = Context.App!;

        if (GoalName != null)
        {
            var goal = await GoalName.GetGoalAsync(app, Context);
            if (goal == null)
                return Error(new ServiceError(
                    $"Goal '{GoalName.Name ?? GoalName.PrPath}' not found", "NotFound", 404));

            return await app.RunGoalAsync(goal, Context, Context.CancellationToken);
        }

        if (Step != null)
            return await Step.RunAsync(app, Context, Actor);

        if (Action != null)
            return await Action.RunAsync(app, Context, Actor);

        return Error(new ActionError(
            "run requires a GoalCall, Step, or Action", "MissingInput", 400));
    }
}
