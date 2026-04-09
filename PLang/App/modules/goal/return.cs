using System.ComponentModel;
using App.Variables;

namespace App.modules.goal;

/// <summary>
/// Returns a value from the current goal. If the value is a failing Data, it propagates the error.
/// </summary>
[Action("return", Cacheable = false)]
public partial class Return : IContext
{
    public partial Data.@this? Data { get; init; }

    [Description("Number of goal levels to exit. 1 = current goal, 2 = current + caller.")]
    [Default(1)]
    public partial int Depth { get; init; }

    public Task<Data.@this> Run()
    {
        var result = this.Data ?? App.Data.@this.Ok();
        result.Returned = true;
        result.ReturnDepth = Depth > 0 ? Depth : 1;
        return Task.FromResult(result);
    }
}
