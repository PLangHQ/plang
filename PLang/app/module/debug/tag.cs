namespace app.module.debug;

/// <summary>
/// Writes tags onto the surrounding (caller's) Call frame — <c>- tag critical=true, owner=checkout</c>;
/// a bare <c>- tag "manual-checkpoint"</c> is the dict <c>{manual-checkpoint: true}</c>.
/// No-op when CallStack.Current is null (executing outside a tracked dispatch).
/// Observability action — Cacheable=false; tags are diagnostic side-effects, not values.
///
/// Tags attach to the CALLER's frame, not this action's own frame: the user's intent
/// is to annotate the surrounding step/goal scope, and the tag-action's own Call pops
/// the moment Start() returns (its Tags would vanish from the live tree before the next
/// assertion could read them).
/// </summary>
[Action("tag", Cacheable = false)]
public partial class Tag : IContext
{
    /// <summary>The tags to merge into the caller's Call.</summary>
    [IsNotNull]
    public partial global::app.data.@this<global::app.type.item.dict.@this> Tags { get; init; }

    public Task<global::app.data.@this> Start()
    {
        // Tag the CALLER's Call, not our own — see class summary. Falls back to Current
        // if there's no caller (we're already at the root, e.g. a single-action scope).
        var target = Context.CallStack?.Current?.Caller ?? Context.CallStack?.Current;
        if (target == null) return Task.FromResult(Context.Ok());
        return Tags.Use(tags =>
        {
            target.Tag(tags, Context);
            return Task.FromResult(Context.Ok());
        });
    }
}
