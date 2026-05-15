using app.Variables;

namespace app.modules.@event;

[System.ComponentModel.Description("Unregister a previously registered lifecycle event binding by its ID")]
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    [IsNotNull]
    public partial Data.@this<string> EventId { get; init; }

    public Task<Data.@this> Run()
    {
        var removed = Context.Events.Unregister(EventId.Value!);
        return Task.FromResult(Data(removed));
    }
}
