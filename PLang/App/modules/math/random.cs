using App.Variables;

namespace App.modules.math;

[Action("random")]
public partial class Random : IContext
{
    [Default(0)]
    public partial Data.@this<int> Min { get; init; }
    [Default(100)]
    public partial Data.@this<int> Max { get; init; }

    public Task<Data.@this> Run()
    {
        var rng = System.Random.Shared;
        var result = rng.Next(Min.Value, Max.Value + 1);
        return Task.FromResult(Data(result));
    }
}
