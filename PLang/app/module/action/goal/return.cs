using System.ComponentModel;
using app.variable;

namespace app.module.action.goal;

/// <summary>
/// Returns a value from the current goal. If the value is a failing Data, it propagates the error.
/// </summary>
[Action("return", Cacheable = false)]
public partial class Return : IContext
{
    public partial data.@this? Data { get; init; }

    [Description("Number of goal levels to exit. 1 = current goal, 2 = current + caller.")]
    [Default(1)]
    public partial data.@this<global::app.type.item.number.@this> Depth { get; init; }

    public Task<data.@this> Run()
    {
        var result = this.Data ?? Context.Ok();
        result.Returned = true;
        // Sync seam — Peek (the .pr literal is in memory); the number lowers
        // itself at the engine's int return-depth slot.
        int depth = (Depth.Peek() as global::app.type.item.number.@this)?.ToInt32() ?? 0;
        result.ReturnDepth = depth > 0 ? depth : 1;
        return Task.FromResult(result);
    }
}
