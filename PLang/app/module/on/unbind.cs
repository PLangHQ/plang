namespace app.module.on;

/// <summary>Takes a binding off its event (the one <c>on.event</c> answered): it fires no more.</summary>
[Action("unbind", Cacheable = false)]
public partial class OnUnbind : IContext
{
    [IsNotNull]
    public partial data.@this<global::app.@event.binding.@this> Binding { get; init; }

    public async Task<data.@this> Start()
    {
        (await Binding.Value())!.Remove();
        return Data();
    }
}
