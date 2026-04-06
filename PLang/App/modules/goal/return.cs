using App.Engine.Variables;

namespace App.modules.goal;

/// <summary>
/// Returns a value from the current goal. If the value is a failing Data, it propagates the error.
/// </summary>
[Action("return", Cacheable = false)]
public partial class Return : IContext
{
    public partial Data? Data { get; init; }

    public Task<Data> Run()
    {
        var result = Data ?? Engine.Variables.Data.Ok();
        // Signal RunSteps to stop iteration — even for successful returns
        result.Returned = true;
        return Task.FromResult(result);
    }
}
