using App.Variables;

namespace App.modules.cache;

/// <summary>
/// Modifier: wraps an action with cache lookup before and cache store after.
/// On a cache hit, the inner delegate is skipped entirely and the cached result
/// is returned (also published as %!data% for the next action in the step).
/// </summary>
[ModuleDescription("Modifier that wraps the preceding action with result caching for a given duration")]
[System.ComponentModel.Description("Cache the result of the preceding action for DurationMs milliseconds, skipping re-execution on hit")]
[Action("wrap", Cacheable = false)]
[Modifier(Order = 2)]
public partial class CacheWrap : IContext, IModifier
{
    [IsNotNull]
    public partial global::App.Data.@this<long> DurationMs { get; init; }
    [Default(false)]
    public partial global::App.Data.@this<bool> Sliding { get; init; }
    public partial global::App.Data.@this<string>? Key { get; init; }

    public Task<global::App.Data.@this> Run() => Task.FromResult(global::App.Data.@this.Ok());

    public Func<Task<global::App.Data.@this>> Wrap(Func<Task<global::App.Data.@this>> next, Actor.Context.@this context)
    {
        var cacheKey = !string.IsNullOrEmpty(Key?.Value) ? Key.Value! : DefaultKey(context);
        var durationMs = DurationMs.Value;
        var sliding = Sliding.Value;

        return async () =>
        {
            var cache = context.App!.Cache;
            var cached = await cache.GetAsync(cacheKey);
            if (cached != null)
            {
                var hit = cached.ShallowClone();
                context.Variables.Set("!data", hit);
                return hit;
            }

            var result = await next();
            if (result.Success)
            {
                await cache.SetAsync(cacheKey, result,
                    new CacheSettings { DurationMs = durationMs, Sliding = sliding });
            }
            return result;
        };
    }

    private static string DefaultKey(Actor.Context.@this context)
    {
        var step = context.Step;
        var goalPath = step?.Goal?.Path ?? "unknown";
        return $"step:{goalPath}:{step?.Index}";
    }
}
