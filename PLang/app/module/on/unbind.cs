namespace app.module.on;

/// <summary>Takes a binding off its event (the one <c>on.event</c> answered): it fires no more. One its maker made, which
/// this app took in at its birth, is refused: it isn't this app's to take off.</summary>
[Action("unbind", Cacheable = false)]
public partial class OnUnbind : IContext
{
    [IsNotNull]
    public partial data.@this<global::app.@event.binding.@this> Binding { get; init; }

    public async Task<data.@this> Start()
    {
        try { (await Binding.Value())!.Remove(); }
        catch (global::app.error.AppException ex) { return Context.Error(ex.Error); }
        return Data();
    }
}
