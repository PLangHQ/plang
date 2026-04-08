using App;
using App.Actor.Context;
using App.Variables;

namespace App.modules.goal;

[Action("call")]
public partial class Call : IContext
{
    public partial GoalCall GoalName { get; init; }

    /// <summary>
    /// Target actor to run the goal on. If null, runs on the current context.
    /// </summary>
    public partial Actor.@this? Actor { get; init; }

    public async Task<Data.@this> Run()
    {
        var app = Context.App!;
        var execContext = Actor?.Context ?? Context;
        var goalResult = await GoalName.GetGoalAsync(app, execContext);
        if (!goalResult.Success) return goalResult;

        return await ((Goals.Goal.@this)goalResult.Value!).RunAsync(execContext);
    }
}
