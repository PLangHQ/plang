using App.Variables;

namespace App.modules.cache;

/// <summary>
/// Stores a step's result in the cache. Collects return variables from the step's actions
/// and caches them with the step's CacheSettings (duration, sliding).
/// Only stores if the step has CacheSettings, result succeeded, and all actions are cacheable.
/// </summary>
[Action("store", Cacheable = false)]
public partial class Store : IContext
{
    [IsNotNull]
    public partial Step Step { get; init; }

    public partial Data Data { get; init; }

    public async Task<Data> Run()
    {
        // No cache settings, no data, or failed result — don't cache
        if (Step.Cache == null || Data == null || !Data.Success) return Data ?? Data.Ok();

        // Any non-cacheable action — don't cache
        var modules = Context.App!.Modules;
        foreach (var action in Step.Actions)
        {
            if (!modules.IsCacheable(action.Module, action.ActionName))
                return Data;
        }

        var key = BuildCacheKey();
        var entry = CollectReturnVariables();

        await Context.App!.Cache.SetAsync(key, entry, Step.Cache);

        return Data;
    }

    private string BuildCacheKey()
    {
        if (!string.IsNullOrEmpty(Step.Cache!.Key))
            return Context.Variables.Resolve(Step.Cache.Key);

        var goalPath = Step.Goal?.Path ?? "unknown";
        return $"step:{goalPath}:{Step.Index}";
    }

    private Data CollectReturnVariables()
    {
        var entry = Data.Ok();
        foreach (var action in Step.Actions)
        {
            if (action.Return == null) continue;
            foreach (var returnVar in action.Return)
            {
                var data = Context.Variables.Get(returnVar.Name);
                if (data != null)
                {
                    entry.Properties[returnVar.Name] = data;
                }
            }
        }
        return entry;
    }
}
