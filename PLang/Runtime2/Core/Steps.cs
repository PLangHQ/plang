using PLang.Runtime2.Context;

namespace PLang.Runtime2.Core;

public sealed class Steps : List<Step>
{
    public Steps() { }
    public Steps(IEnumerable<Step> steps) : base(steps) { }

    public List<Step> Value => this;

    public async Task Load(PLangContext context)
    {
        foreach (var step in this)
            await step.Load(context);
    }
}
