using app.variable;

namespace app.module.@event;

[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    [IsNotNull]
    public partial data.@this<global::app.type.text.@this> EventId { get; init; }

    public async Task<data.@this<global::app.type.@bool.@this>> Run()
    {
        var removed = Context.Events.Unregister((await EventId.Value())!.Clr<string>()!);
        return Context.Ok<global::app.type.@bool.@this>(removed);
    }
}
