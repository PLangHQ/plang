namespace app.module.debug;

/// <summary>
/// Writes tags onto the running goal's frame — <c>- tag critical=true, owner=checkout</c>;
/// a bare <c>- tag "manual-checkpoint"</c> is the dict <c>{manual-checkpoint: true}</c>.
/// Read back as <c>%!callStack.Scope.Tags.critical%</c>. No-op outside any frame.
/// Observability action — Cacheable=false; tags are diagnostic side-effects, not values.
///
/// Tags attach to the goal's frame (<c>CallStack.Scope</c>), not the tag action's or its step's:
/// those pop when the step ends, before a later step could read them. An action run outside any
/// goal tags the frame it runs in.
/// </summary>
[Action("tag", Cacheable = false)]
public partial class Tag : IContext
{
    /// <summary>The tags to merge into the caller's Call.</summary>
    [IsNotNull]
    public partial global::app.data.@this<global::app.type.item.dict.@this> Tags { get; init; }

    public Task<global::app.data.@this> Start()
    {
        // The goal's frame — see class summary; outside a goal, the frame this runs in.
        var target = Context.CallStack?.Scope ?? Context.CallStack?.Current;
        if (target == null) return Task.FromResult(Context.Ok());
        return Tags.Use(tags =>
        {
            target.Tag(tags, Context);
            return Task.FromResult(Context.Ok());
        });
    }
}
