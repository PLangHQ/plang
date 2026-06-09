using app.variable;

namespace app.module.@event;

[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    [IsNotNull]
    public partial data.@this<global::app.type.text.@this> EventId { get; init; }

    public Task<data.@this<global::app.type.@bool.@this>> Run()
    {
        var removed = Context.Events.Unregister(EventId.Materialize() as global::app.type.text.@this);
        return Task.FromResult(global::app.data.@this<global::app.type.@bool.@this>.Ok(removed));
    }
}
