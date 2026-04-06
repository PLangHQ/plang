using App.Variables;

namespace App.modules.cache;

/// <summary>
/// Checks the cache for a step's result. Returns cached Data if hit, null if miss.
/// Returns null immediately if any action in the step has Cacheable = false,
/// or if the step has no CacheSettings.
/// </summary>
[Action("check", Cacheable = false)]
public partial class Check : IContext
{
    [IsNotNull]
    public partial Step Step { get; init; }

    public async Task<Data.@this> Run()
    {
        // No cache settings on the step — skip (miss)
        if (Step.Cache == null) return App.Data.@this.Ok(false);

        // Any non-cacheable action — skip (miss)
        var modules = Context.App!.Modules;
        foreach (var action in Step.Actions)
        {
            if (!modules.IsCacheable(action.Module, action.ActionName))
                return App.Data.@this.Ok(false);
        }

        var key = BuildCacheKey();
        var cached = await Context.App!.Cache.GetAsync(key);

        // Miss — return false
        if (cached == null) return App.Data.@this.Ok(false);

        // Hit — restore return variables and return true
        foreach (var data in cached.Properties)
        {
            Context.Variables.Set(data.Name, data.Value, data.Type);
        }

        return App.Data.@this.Ok(true);
    }

    private string BuildCacheKey()
    {
        if (!string.IsNullOrEmpty(Step.Cache!.Key))
            return Context.Variables.Resolve(Step.Cache.Key);

        var goalPath = Step.Goal?.Path ?? "unknown";
        return $"step:{goalPath}:{Step.Index}";
    }
}
