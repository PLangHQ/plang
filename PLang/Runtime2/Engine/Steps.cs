using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine;

public sealed class Steps : List<Step>
{
    public Steps() { }
    public Steps(IEnumerable<Step> steps) : base(steps) { }

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
