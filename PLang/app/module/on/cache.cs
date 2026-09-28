namespace app.module.on;

/// <summary>
/// <c>on.cache</c> — a clause of the action before it: its result is cached. Before each attempt starts, a cached
/// result answers in its place (the work is skipped, and the answer is <c>%!data%</c>); after an attempt that did
/// the work, a success is stored. A recovery's result is never cached — recovery runs on the error outcome, after
/// the attempt.
/// </summary>
[Action("cache", Cacheable = false)]
public partial class OnCache : IContext, IClause
{
    /// <summary>How long a result is kept.</summary>
    [IsNotNull]
    public partial global::app.data.@this<global::app.type.item.duration.@this> Duration { get; init; }
    [Default(false)]
    public partial global::app.data.@this<global::app.type.item.@bool.@this> Sliding { get; init; }
    public partial global::app.data.@this<global::app.type.item.text.@this>? Key { get; init; }

    public Task<global::app.data.@this> Start() => Task.FromResult(Context.Ok());

    public void Bind(global::app.goal.step.action.@this clause, global::app.goal.step.action.@this action)
    {
        var on = action.Own();
        on.Bind("start", global::app.@event.When.before, side => new global::app.@event.binding.clause.@this(
            side, clause, action, (handler, _, result, context) => ((OnCache)handler).Begin(result, context)));
        on.Bind("start", global::app.@event.When.after, side => new global::app.@event.binding.clause.@this(
            side, clause, action, (handler, _, result, context) => ((OnCache)handler).End(result, context)));
    }

    // What this attempt's lookup found, held on the action's frame from before its start to after it.
    private sealed record Lookup(string Key, bool Hit);

    /// <summary>Before an attempt starts: a cached result answers in its place (handled — the work is skipped);
    /// a miss lets it run.</summary>
    public async Task<global::app.data.@this> Begin(global::app.data.@this result, actor.context.@this context)
    {
        // The handler USES the key — the door, not Peek: an authored template key ("user-%id%") renders per
        // run here; Peek would hand the literal holes and every user would share one entry.
        var keyText = Key == null ? null : await Key.Value();
        string key = keyText?.IsTruthy() == true ? keyText.ToString() : DefaultKey(context);
        var cached = await context.App!.Cache.GetAsync(key);
        context.CallStack.Current?.SetItem(new Lookup(key, cached != null));
        if (cached == null) return result;
        var hit = cached.Copy();
        hit.Handled = true;
        return hit;
    }

    /// <summary>After an attempt: the success of the work it did is stored; a hit (the work skipped) stores
    /// nothing.</summary>
    public async Task<global::app.data.@this> End(global::app.data.@this result, actor.context.@this context)
    {
        var frame = context.CallStack.Current;
        if (frame?.GetItem<Lookup>() is not { Hit: false } lookup || !result.Success) return result;
        frame.SetItem(lookup with { Hit = true });   // stored — this attempt's lookup is spent
        // A lazy reference result (file/url/image) caches with its CONTENT in memory — a hit must not re-read
        // the source; that is the point of the cache. .Value() is the materialize door (idempotent).
        await result.Value();
        var duration = (System.TimeSpan)(await Duration.Value())!;
        var sliding = (await Sliding.Value())?.IsTruthy() ?? false;
        await context.App!.Cache.SetAsync(lookup.Key, result,
            new CacheSettings { DurationMs = (long)duration.TotalMilliseconds, Sliding = sliding });
        return result;
    }

    private static string DefaultKey(actor.context.@this context)
    {
        var step = context.CallStack.Step;
        var goalPath = step?.Goal?.Path?.ToString() ?? "unknown";
        return $"step:{goalPath}:{step?.Index}";
    }
}
