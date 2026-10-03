namespace app.module.timer;

/// <summary>
/// Pauses execution for Duration. Respects the current cancellation token,
/// so a parent timeout or cancellation aborts the delay.
/// </summary>
[Action("sleep", Cacheable = false)]
public partial class Sleep : IContext
{
    [IsNotNull]
    public partial data.@this<global::app.type.item.duration.@this> Duration { get; init; }

    public async Task<global::app.data.@this> Start()
    {
        await Task.Delay((await Duration.Value())!, Context.CancellationToken);
        return Context.Ok();
    }
}
