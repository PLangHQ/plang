using App;
using App.Actor.Context;
using App.Variables;

namespace App.modules.goal;

/// <summary>
/// Calls a named goal, optionally on a different actor.
/// Parameters are injected into the target goal's context by the GoalCall resolver.
/// </summary>
[Action("call")]
public partial class Call : IContext
{
    public partial Data.@this<GoalCall> GoalName { get; init; }

    /// <summary>
    /// Target actor to run the goal on. If null, runs on the current context.
    /// </summary>
    public partial Data.@this<Actor.@this>? Actor { get; init; }

    public async Task<Data.@this> Run()
    {
        var execContext = Actor?.Value?.Context ?? Context;
        return await Context.App!.RunGoalAsync(GoalName.Value!, execContext);
    }
}
