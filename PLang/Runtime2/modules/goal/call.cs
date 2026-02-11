using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.goal;

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
