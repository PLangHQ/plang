using App.Variables;

namespace App.modules.@event;

[Example("remove event %eventId%", "EventId=%eventId%")]
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    [IsNotNull]
    public partial string EventId { get; init; }

    public Task<Data.@this> Run()
    {
        var removed = Context.Events.Unregister(EventId);
        return Task.FromResult(App.Data.@this.Ok(removed));
    }
}
