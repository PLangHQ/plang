using app.variable;

namespace app.module.cache;

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
    public partial global::app.data.@this<global::app.type.number.@this> DurationMs { get; init; }
    [Default(false)]
    public partial global::app.data.@this<global::app.type.@bool.@this> Sliding { get; init; }
    public partial global::app.data.@this<global::app.type.text.@this>? Key { get; init; }

    public Task<global::app.data.@this> Run() => Task.FromResult(global::app.data.@this.Ok());

    public Func<Task<global::app.data.@this>> Wrap(Func<Task<global::app.data.@this>> next, actor.context.@this context)
    {
        string cacheKey = !string.IsNullOrEmpty(Key?.Value) ? (string)Key.Value! : DefaultKey(context);
        long durationMs = DurationMs.GetValue<long>();
        var sliding = Sliding.Value;

        return async () =>
        {
            var cache = context.App!.Cache;
            var cached = await cache.GetAsync(cacheKey);
            if (cached != null)
            {
                var hit = cached.ShallowClone();
                context.Variable.Set("!data", hit);
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
        var goalPath = step?.Goal?.Path?.ToString() ?? "unknown";
        return $"step:{goalPath}:{step?.Index}";
    }
}
