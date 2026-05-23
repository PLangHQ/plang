using app.variables;

namespace app.modules.@event;

[System.ComponentModel.Description("Unregister a previously registered lifecycle event binding by its ID")]
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
