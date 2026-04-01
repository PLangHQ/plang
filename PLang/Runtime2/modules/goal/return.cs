using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.goal;

/// <summary>
/// Returns a value from the current goal. If the value is a failing Data, it propagates the error.
/// </summary>
[Action("return", Cacheable = false)]
public partial class Return : IContext
{
    public partial Data? Data { get; init; }

    public Task<Data> Run()
    {
        // Return the Data as-is — if it has an error, it propagates (RunSteps stops on !Success)
        return Task.FromResult(Data ?? Engine.Memory.Data.Ok());
    }
}
