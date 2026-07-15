using app.variable;

namespace app.module.action.cache;

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
    public partial global::app.data.@this<global::app.type.item.number.@this> DurationMs { get; init; }
    [Default(false)]
    public partial global::app.data.@this<global::app.type.item.@bool.@this> Sliding { get; init; }
    public partial global::app.data.@this<global::app.type.item.text.@this>? Key { get; init; }

    public Task<global::app.data.@this> Run() => Task.FromResult(Context.Ok());

    public Func<Task<global::app.data.@this>> Wrap(Func<Task<global::app.data.@this>> next, actor.context.@this context)
    {
        return async () =>
        {
            // The handler USES these values — the door, not Peek. An authored
            // template key ("user-%id%") renders per execution here; Peek
            // would hand the literal holes and every user would share one
            // cache entry.
            var keyText = Key == null ? null : await Key.Value();
            string cacheKey = keyText?.IsTruthy() == true ? keyText.ToString() : DefaultKey(context);
            long durationMs = (await DurationMs.Value())?.ToInt64() ?? 0;
            var sliding = (await Sliding.Value())?.IsTruthy() ?? false;
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
                // A lazy reference result (file/url/image) caches with its CONTENT
                // in memory — a cache hit must not re-read the source; that is the
                // point of the cache. .Value() is the materialize door (idempotent);
                // a non-reference result resolves cheaply to itself.
                await result.Value();
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
