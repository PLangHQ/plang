using app.variable;

namespace app.module.action.math;

[Action("random")]
public partial class Random : IContext
{
    [Default(0)]
    public partial data.@this<global::app.type.item.number.@this> Min { get; init; }
    [Default(100)]
    public partial data.@this<global::app.type.item.number.@this> Max { get; init; }

    public async Task<data.@this<global::app.type.item.number.@this>> Run()
    {
        // Typed read; the number converts ITSELF at the .NET boundary — the
        // widest overload Random offers (NextInt64), so long ranges fit.
        var min = (await Min.Value())!;
        var max = (await Max.Value())!;
        var rng = System.Random.Shared;
        long result = rng.NextInt64(min.ToInt64(), max.ToInt64() + 1);
        return Context.Ok<global::app.type.item.number.@this>(result);
    }
}
