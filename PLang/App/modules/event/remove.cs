using App.Variables;

namespace App.modules.@event;

[Example("remove event %eventId%", "EventId=%eventId%")]
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
