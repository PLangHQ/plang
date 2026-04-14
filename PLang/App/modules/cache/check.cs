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
    public partial Data.@this<Step> Step { get; init; }

    public async Task<Data.@this> Run()
    {
        // No cache settings on the step — skip (miss)
        var step = Step.Value!;
        if (step.Cache == null) return Data(false);

        // Any non-cacheable action — skip (miss)
        var modules = Context.App!.Modules;
        foreach (var action in step.Actions)
        {
            if (!modules.IsCacheable(action.Module, action.ActionName))
                return Data(false);
        }

        var key = BuildCacheKey();
        var cached = await Context.App!.Cache.GetAsync(key);

        // Miss — return false
        if (cached == null) return Data(false);

        // Hit — restore return variables and return true
        foreach (var data in cached.Properties)
        {
            Context.Variables.Set(data.Name, data.Value, data.Type);
        }

        return Data(true);
    }

    private string BuildCacheKey()
    {
        var step = Step.Value!;
        if (!string.IsNullOrEmpty(step.Cache!.Key))
            return Context.Variables.Resolve(step.Cache.Key);

        var goalPath = step.Goal?.Path ?? "unknown";
        return $"step:{goalPath}:{step.Index}";
    }
}
