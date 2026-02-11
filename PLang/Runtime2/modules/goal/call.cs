using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.goal;

[Action("call")]
public partial class Call : IContext
{
    public partial string GoalName { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine!;
        var goal = await engine.Goals.GetAsync(GoalName);
        if (goal == null)
            return Data.Fail(new Errors.Error($"Goal '{GoalName}' not found"));

        return await engine.RunGoalAsync(goal, Context);
    }
}
