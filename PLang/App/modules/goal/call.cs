using App;
using App.Context;
using App.Variables;

namespace App.modules.goal;

[Action("call")]
public partial class Call : IContext
{
    public partial GoalCall GoalName { get; init; }

    /// <summary>
    /// Target actor to run the goal on. If null, runs on the current context.
    /// </summary>
    public partial Actor? Actor { get; init; }

    public async Task<Data.@this> Run()
    {
        var engine = Context.App!;
        var execContext = Actor?.Context ?? Context;
        var goal = await GoalName.GetGoalAsync(engine, execContext);
        if (goal == null)
            return App.Data.@this.FromError(new Errors.ServiceError(
                $"Goal '{GoalName.Name}' not found", "NotFound", 404));

        return await engine.RunGoalAsync(goal, execContext);
    }
}
