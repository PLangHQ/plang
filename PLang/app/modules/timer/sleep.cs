namespace app.modules.timer;

/// <summary>
/// Pauses execution for Ms milliseconds. Respects the current cancellation token,
/// so a parent timeout or cancellation aborts the delay.
/// </summary>
[System.ComponentModel.Description("Pause execution for Ms milliseconds, honouring the current cancellation token")]
[Action("sleep", Cacheable = false)]
public partial class Sleep : IContext
{
    [IsNotNull]
    public partial data.@this<int> Ms { get; init; }

    public async Task<global::app.data.@this> Run()
    {
        await Task.Delay(Ms.Value, Context.CancellationToken);
        return global::app.data.@this.Ok();
    }
}
