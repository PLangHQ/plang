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
        var result = await engine.RunGoalAsync(GoalName, Context, Context.CancellationToken);
        if (!result.Success) return result;

        // If goal succeeded but returned no value, check __stepResult for the last step's output
        if (result.Value == null)
        {
            var lastStepResult = Context.MemoryStack.Get("__stepResult");
            if (lastStepResult?.Value != null)
                return Data.Ok(lastStepResult.Value, lastStepResult.Type);
        }

        return result;
    }
}
