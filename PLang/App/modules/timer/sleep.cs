namespace App.modules.timer;

/// <summary>
/// Pauses execution for Ms milliseconds. Respects the current cancellation token,
/// so a parent timeout or cancellation aborts the delay.
/// </summary>
[Action("sleep", Cacheable = false)]
public partial class Sleep : IContext
{
    [IsNotNull]
    public partial Data.@this<int> Ms { get; init; }

    public async Task<global::App.Data.@this> Run()
    {
        await Task.Delay(Ms.Value, Context.CancellationToken);
        return global::App.Data.@this.Ok();
    }
}
