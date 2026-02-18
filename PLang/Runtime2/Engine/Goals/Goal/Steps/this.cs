using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.Goals.Goal.Steps;

public sealed class @this : List<Step.@this>
{
    public @this() { }
    public @this(IEnumerable<Step.@this> steps) : base(steps) { }

    public List<Step.@this> Value => this;

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
