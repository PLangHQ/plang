using app.variable;

namespace app.module.math;

[Action("random")]
public partial class Random : IContext
{
    [Default(0)]
    public partial data.@this<global::app.type.number.@this> Min { get; init; }
    [Default(100)]
    public partial data.@this<global::app.type.number.@this> Max { get; init; }

    public Task<data.@this<object>> Run()
    {
        var rng = System.Random.Shared;
        var result = rng.Next(Min.GetValue<int>(), Max.GetValue<int>() + 1);
        return Task.FromResult(data.@this<object>.Ok(result));
    }
}
