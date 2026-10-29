namespace app.module.on;

/// <summary>Takes an event off its moment (the one <c>on.event</c> answered): it runs no more. One its maker made, which
/// this app took in at its birth, is refused: it isn't this app's to take off.</summary>
[Action("unbind", Cacheable = false)]
public partial class OnUnbind : IContext
{
    [IsNotNull]
    public partial data.@this<global::app.@event.@this> Event { get; init; }

    public async Task<data.@this> Start()
    {
        try { (await Event.Value())!.Remove(); }
        catch (global::app.error.AppException ex) { return Context.Error(ex.Error); }
        return Data();
    }
}
