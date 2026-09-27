namespace app.module.action.@event;

/// <summary>Removes the binding event.on answered the id of — and the <c>on.start</c> binding it stands for.</summary>
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    [IsNotNull]
    public partial data.@this<global::app.type.item.text.@this> EventId { get; init; }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Start()
    {
        var id = (await EventId.Value())!.Clr<string>()!;
        var registered = Context.Events.list.FirstOrDefault(b => b.Id == id);
        foreach (var bound in registered?.Targets.OfType<global::app.@event.binding.@this>() ?? [])
            bound.Remove();
        var removed = Context.Events.Unregister(id);
        return Context.Ok<global::app.type.item.@bool.@this>(removed);
    }
}
