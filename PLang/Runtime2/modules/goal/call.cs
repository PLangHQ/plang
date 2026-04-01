using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.goal;

[Action("call")]
public partial class Call : IContext
{
    public partial GoalCall GoalName { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine!;

        // 1. Check sub-goals in current goal (already in memory)
        var currentGoal = Context.Goal;
        Console.WriteLine($"[goal.call] looking for '{GoalName.Name}' in goal '{currentGoal?.Name}' with {currentGoal?.Goals?.Count ?? 0} sub-goals");
        if (currentGoal != null)
        {
            var subGoal = currentGoal.Goals.FirstOrDefault(g =>
                string.Equals(g.Name, GoalName.Name, StringComparison.OrdinalIgnoreCase));
            if (subGoal != null)
                return await engine.RunGoalAsync(subGoal, Context);
        }

        // 2. Not a sub-goal — load from .pr file
        var goal = await GoalName.GetGoalAsync(engine, Context);
        if (goal == null)
            return Data.FromError(new Engine.Errors.ServiceError(
                $"Goal '{GoalName.Name}' not found", "NotFound", 404));

        return await engine.RunGoalAsync(goal, Context);
    }
}
