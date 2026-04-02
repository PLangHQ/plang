using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.runtime;

/// <summary>
/// Runs a goal through the full RunStep pipeline (engine.execute, error.check, events, caching).
/// Accepts a Goal object or GoalCall. Sets %goal% on MemoryStack, then calls RunGoal in run.pr.
/// Always marks result as Handled — callers decide what to do with errors.
///
/// ContextMode controls isolation:
///   - null/empty: runs on the current context (shared state)
///   - "child": saves/restores user actor state (events + variables) around execution
///   - "new": creates a fresh engine (full isolation) — not yet implemented
/// </summary>
[Action("run")]
public partial class run : IContext
{
    public partial Engine.Goals.Goal.@this? Goal { get; init; }
    public partial GoalCall? GoalName { get; init; }
    [Default("")]
    public partial string ContextMode { get; init; }

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

        var isChild = string.Equals(ContextMode, "child", StringComparison.OrdinalIgnoreCase);

        // Save user actor state before execution (child mode = isolation)
        var userContext = engine.User.Context;
        var savedEvents = isChild ? userContext.User.Events.Save() : null;
        var savedMemory = isChild ? userContext.MemoryStack.Save() : null;

        try
        {
            Context.MemoryStack.Put(new Data("goal", goal!));

            var runGoalCall = new GoalCall { Name = "RunGoal", PrPath = "/system/.build/run.pr" };
            var runResult = await engine.RunGoalAsync(runGoalCall, Context);
            runResult.Handled = true;
            runResult.Name = goal?.Name ?? GoalName?.Name ?? GoalName?.PrPath ?? "";

            // Add result to testResults directly if in test mode.
            var testResults = engine.System.Context.MemoryStack.GetValue("testResults") as List<object?>;
            testResults?.Add(runResult);

            return runResult;
        }
        finally
        {
            // Restore user actor state (child mode)
            if (savedEvents != null)
                userContext.User.Events.Restore(savedEvents);
            if (savedMemory != null)
                userContext.MemoryStack.Restore(savedMemory);
        }
    }
}
