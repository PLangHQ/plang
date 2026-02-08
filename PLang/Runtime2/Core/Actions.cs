using PLang.Runtime2.Context;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Core;

public sealed class Actions : List<Action>
{
    public Actions() { }
    public Actions(IEnumerable<Action> actions) : base(actions) { }

    public List<Action> Value => this;

    public async Task Load(PLangContext context)
    {
        foreach (var action in this)
            await action.Load(context);
    }

    public async Task<Data> RunAsync(Engine engine, PLangContext context, CancellationToken ct = default)
    {
        Data merged = Data.Ok();
        foreach (var action in this)
        {
            var result = await action.RunAsync(engine, context, ct);
            if (!result.Success) return result;
            merged = merged.Merge(result);
        }
        return merged;
    }
}
