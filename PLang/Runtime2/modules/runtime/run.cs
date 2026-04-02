using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.runtime;

/// <summary>
/// Runs a goal through the full RunStep pipeline (engine.execute, error.check, events, caching).
/// Accepts a Goal object or GoalCall. Sets %goal% on MemoryStack, then calls RunGoal in run.pr.
/// Always marks result as Handled — callers decide what to do with errors.
/// </summary>
[Action("run")]
public partial class run : IContext
{
    public partial Engine.Goals.Goal.@this? Goal { get; init; }
    public partial GoalCall? GoalName { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine!;

        // Resolve goal from GoalCall if Goal not provided directly
        var goal = Goal;
        if (goal == null && GoalName != null)
        {
            goal = await GoalName.GetGoalAsync(engine, Context);
            if (goal == null)
            {
                var result = Data.FromError(new Engine.Errors.ServiceError(
                    $"Goal '{GoalName.Name ?? GoalName.PrPath}' not found", "NotFound", 404));
                result.Handled = true;
                return result;
            }
        }

        if (goal != null)
            Context.MemoryStack.Put(new Data("goal", goal));

        var runGoalCall = new GoalCall { Name = "RunGoal", PrPath = "/system/.build/run.pr" };
        var runResult = await engine.RunGoalAsync(runGoalCall, Context);
        runResult.Handled = true;
        return runResult;
    }
}
