using app.variable;

namespace app.module.@event;

[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    [IsNotNull]
    public partial data.@this<string> EventId { get; init; }

    public Task<data.@this<bool>> Run()
    {
        var removed = Context.Events.Unregister(EventId.Value!);
        return Task.FromResult(global::app.data.@this<bool>.Ok(removed));
    }
}
