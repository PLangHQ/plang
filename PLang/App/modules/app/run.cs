using App.Variables;

namespace App.modules.app;

/// <summary>
/// Runs a goal through the full RunStep pipeline (events, caching, error handling).
/// Accepts a GoalCall, resolves the goal, then delegates to app.RunGoalAsync.
/// Callers decide what to do with errors.
/// </summary>
[Action("run")]
public partial class run : IContext
{
    public partial GoalCall GoalName { get; init; }

    public async Task<Data.@this> Run()
    {
        var app = Context.App!;

        var goal = await GoalName.GetGoalAsync(app, Context);
        if (goal == null)
            return Error(new Errors.ServiceError(
                $"Goal '{GoalName.Name ?? GoalName.PrPath}' not found", "NotFound", 404));

        return await app.RunGoalAsync(goal, Context, Context.CancellationToken);
    }
}
