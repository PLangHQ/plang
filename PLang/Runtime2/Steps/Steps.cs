using PLang.Runtime2.Context;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2;

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
