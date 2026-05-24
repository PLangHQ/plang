using app.variables;

namespace app.modules.cache;

/// <summary>
/// Modifier: wraps an action with cache lookup before and cache store after.
/// On a cache hit, the inner delegate is skipped entirely and the cached result
/// is returned (also published as %!data% for the next action in the step).
/// </summary>
[Action("wrap", Cacheable = false)]
[Modifier(Order = 2)]
public partial class CacheWrap : IContext, IModifier
{
    [IsNotNull]
    public partial global::app.data.@this<long> DurationMs { get; init; }
    [Default(false)]
    public partial global::app.data.@this<bool> Sliding { get; init; }
    public partial global::app.data.@this<string>? Key { get; init; }

    public Task<global::app.data.@this> Run() => Task.FromResult(global::app.data.@this.Ok());

    public Func<Task<global::app.data.@this>> Wrap(Func<Task<global::app.data.@this>> next, actor.context.@this context)
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

    private static string DefaultKey(actor.context.@this context)
    {
        var step = context.Step;
        var goalPath = step?.Goal?.Path ?? "unknown";
        return $"step:{goalPath}:{step?.Index}";
    }
}
