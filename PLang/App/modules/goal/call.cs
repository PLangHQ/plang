using App.Engine;
using App.Engine.Context;
using App.Engine.Variables;

namespace App.modules.goal;

[Action("call")]
public partial class Call : IContext
{
    public partial GoalCall GoalName { get; init; }

    /// <summary>
    /// Target actor to run the goal on. If null, runs on the current context.
    /// </summary>
    public partial Actor? Actor { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine!;
        var execContext = Actor?.Context ?? Context;
        var goal = await GoalName.GetGoalAsync(engine, execContext);
        if (goal == null)
            return Data.FromError(new Engine.Errors.ServiceError(
                $"Goal '{GoalName.Name}' not found", "NotFound", 404));

        return await engine.RunGoalAsync(goal, execContext);
    }
}
