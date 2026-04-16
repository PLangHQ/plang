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

    public partial Data.@this Data { get; init; }

    public async Task<Data.@this> Run()
    {
        // No cache settings, no data, or failed result — don't cache
        if (Step.Cache == null || this.Data == null || !this.Data.Success) return this.Data ?? App.Data.@this.Ok();

        // Any non-cacheable action — don't cache
        var modules = Context.App!.Modules;
        foreach (var action in Step.Actions)
        {
            if (!modules.IsCacheable(action.Module, action.ActionName))
                return this.Data;
        }

        var key = BuildCacheKey();
        var entry = CollectReturnVariables();

        await Context.App!.Cache.SetAsync(key, entry, Step.Cache);

        return this.Data;
    }

    private string BuildCacheKey()
    {
        if (!string.IsNullOrEmpty(Step.Cache!.Key))
            return Context.Variables.Resolve(Step.Cache.Key);

        var goalPath = Step.Goal?.Path ?? "unknown";
        return $"step:{goalPath}:{Step.Index}";
    }

    private App.Data.@this CollectReturnVariables()
    {
        // __data__ holds the last action's result — that's what we cache
        return Context.Variables.Get("__data__") ?? App.Data.@this.Ok();
    }
}
