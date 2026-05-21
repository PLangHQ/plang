using System.ComponentModel;
using app.variables;

namespace app.modules.goal;

/// <summary>
/// Returns a value from the current goal. If the value is a failing Data, it propagates the error.
/// </summary>
[System.ComponentModel.Description("Return early from the current goal, propagating Data as the goal result")]
[Action("return", Cacheable = false)]
public partial class Return : IContext
{
    public partial data.@this? Data { get; init; }

    [Description("Number of goal levels to exit. 1 = current goal, 2 = current + caller.")]
    [Default(1)]
    public partial data.@this<int> Depth { get; init; }

    public Task<data.@this> Run()
    {
        var result = this.Data ?? global::app.data.@this.Ok();
        result.Returned = true;
        result.ReturnDepth = Depth.Value > 0 ? Depth.Value : 1;
        return Task.FromResult(result);
    }
}
