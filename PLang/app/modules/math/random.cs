using app.variables;

namespace app.modules.math;

[Action("random")]
public partial class Random : IContext
{
    [Default(0)]
    public partial data.@this<int> Min { get; init; }
    [Default(100)]
    public partial data.@this<int> Max { get; init; }

    public Task<data.@this<object>> Run()
    {
        var rng = System.Random.Shared;
        var result = rng.Next(Min.Value, Max.Value + 1);
        return Task.FromResult(data.@this<object>.Ok(result));
    }
}
