using App;
using App.Context;
using App.Goals.Goal;
using App.Variables;

namespace App.modules.runtime;

/// <summary>
/// Runs a goal through the full RunStep pipeline (app.execute, error.check, events, caching).
/// Accepts a Goal object or GoalCall. Sets %goal% on Variables, then calls RunGoal in run.pr.
/// Always marks result as Handled — callers decide what to do with errors.
///
/// ContextMode controls isolation:
///   - null/empty: runs on the current context (shared state)
///   - "child": saves/restores user actor state (events + variables) around execution
///   - "new": creates a fresh app (full isolation) — not yet implemented
/// </summary>
[Action("run")]
public partial class run : IContext
{
    public partial Goals.Goal.@this? Goal { get; init; }
    public partial GoalCall? GoalName { get; init; }
    [Default("")]
    public partial string ContextMode { get; init; }

    public async Task<Data.@this> Run()
    {
        var engine = Context.App!;

        // Resolve goal from GoalCall if Goal not provided directly
        var goal = Goal;
        if (goal == null && GoalName != null)
        {
            goal = await GoalName.GetGoalAsync(engine, Context);
            if (goal == null)
            {
                var result = Data.@this.FromError(new Errors.ServiceError(
                    $"Goal '{GoalName.Name ?? GoalName.PrPath}' not found", "NotFound", 404));
                result.Handled = true;
                return result;
            }
        }

        var isChild = string.Equals(ContextMode, "child", StringComparison.OrdinalIgnoreCase);

        // Save user actor state before execution (child mode = isolation)
        var execContext = engine.Context;
        var savedEvents = isChild ? execContext.Events.Save() : null;
        var savedMemory = isChild ? execContext.Variables.Save() : null;

        try
        {
            Context.Variables.Put(new Data("goal", goal!));

            var runGoalCall = new GoalCall { Name = "RunGoal", PrPath = "/system/.build/run.pr" };
            var runResult = await engine.RunGoalAsync(runGoalCall, Context);
            runResult.Handled = true;
            runResult.Name = goal?.Name ?? GoalName?.Name ?? GoalName?.PrPath ?? "";

            // Add result to testResults directly if in test mode.
            var testResults = engine.System.Context.Variables.GetValue("testResults") as List<object?>;
            testResults?.Add(runResult);

            return runResult;
        }
        finally
        {
            // Restore user actor state (child mode)
            if (savedEvents != null)
                execContext.Events.Restore(savedEvents);
            if (savedMemory != null)
                execContext.Variables.Restore(savedMemory);
        }
    }
}
