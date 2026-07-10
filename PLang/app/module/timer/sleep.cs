namespace app.module.timer;

/// <summary>
/// Pauses execution for Ms milliseconds. Respects the current cancellation token,
/// so a parent timeout or cancellation aborts the delay.
/// </summary>
[Action("sleep", Cacheable = false)]
public partial class Sleep : IContext
{
    [IsNotNull]
    public partial data.@this<global::app.type.item.number.@this> Ms { get; init; }

    public async Task<global::app.data.@this> Run()
    {
        await Task.Delay((await Ms.Value())!.ToInt32(), Context.CancellationToken);
        return Context.Ok();
    }
}
