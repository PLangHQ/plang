using System.ComponentModel;
using app.variable;

namespace app.module.goal;

/// <summary>
/// Returns a value from the current goal. If the value is a failing Data, it propagates the error.
/// </summary>
[Action("return", Cacheable = false)]
public partial class Return : IContext
{
    public partial data.@this? Data { get; init; }

    [Description("Number of goal levels to exit. 1 = current goal, 2 = current + caller.")]
    [Default(1)]
    public partial data.@this<global::app.type.number.@this> Depth { get; init; }

    public Task<data.@this> Run()
    {
        var result = this.Data ?? global::app.data.@this.Ok();
        result.Returned = true;
        result.ReturnDepth = Depth.GetValue<int>() > 0 ? Depth.GetValue<int>() : 1;
        return Task.FromResult(result);
    }
}
