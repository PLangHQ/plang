using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.@event;

[Example("remove event %eventId%", "EventId=%eventId%")]
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    [IsNotNull]
    public partial string EventId { get; init; }

    public Task<Data> Run()
    {
        var removed = Context.User.Events.Unregister(EventId);
        return Task.FromResult(Data.Ok(removed));
    }
}
