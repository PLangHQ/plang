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
        var goal = await GoalName.GetGoalAsync(engine, Context);
        if (goal == null)
            return Data.FromError(new Engine.Errors.ServiceError(
                $"Goal '{GoalName.Name}' not found", "NotFound", 404));

        return await engine.RunGoalAsync(goal, Context);
    }
}
