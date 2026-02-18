using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.Goals.Steps;

public sealed class GoalSteps : List<Step>
{
    public GoalSteps() { }
    public GoalSteps(IEnumerable<Step> steps) : base(steps) { }

    public List<Step> Value => this;

    public async Task<Data> Load(PLangContext context)
    {
        foreach (var step in this)
        {
            var result = await step.Load(context);
            if (!result.Success) return result;
        }
        return Data.Ok();
    }
}
