using App.Variables;

namespace App.modules.math;

[Action("random")]
public partial class Random : IContext
{
    [Default(0)]
    public partial int Min { get; init; }
    [Default(100)]
    public partial int Max { get; init; }

    public Task<Data.@this> Run()
    {
        var rng = System.Random.Shared;
        var result = rng.Next(Min, Max + 1);
        return Task.FromResult(App.Data.@this.Ok(result));
    }
}
