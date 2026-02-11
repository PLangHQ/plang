using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.@event;

[Action("remove")]
public partial class Remove : IContext
{
    public partial string EventId { get; init; }

    public Task<Data> Run()
    {
        var removed = Context.User.Events.Unregister(EventId);
        return Task.FromResult(Data.Ok(new types.@event
        {
            id = EventId,
            type = "remove"
        }));
    }
}
