using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.goal;

[Action("call")]
public partial class Call : IContext
{
    public partial GoalCall GoalName { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine!;
        return await engine.RunGoalAsync(GoalName, Context, Context.CancellationToken);
    }
}
