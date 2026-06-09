using app.variable;
using number = global::app.type.number.@this;
using POverflow = global::app.type.number.OverflowMode;
using PPrecision = global::app.type.number.PrecisionMode;

namespace app.module.math;

[Action("max")]
public partial class Max : IContext
{
    public partial data.@this A { get; init; }
    public partial data.@this B { get; init; }

    public partial data.@this<global::app.type.choice.@this<POverflow>>? Overflow { get; init; }
    public partial data.@this<global::app.type.choice.@this<PPrecision>>? Precision { get; init; }

    public Task<data.@this<number>> Run()
    {
        var policy = MathPolicy.Resolve(Context, (Overflow?.Materialize() as global::app.type.choice.@this<POverflow>)?.Value, (Precision?.Materialize() as global::app.type.choice.@this<PPrecision>)?.Value);
        var an = number.FromObject(A.Materialize());
        var bn = number.FromObject(B.Materialize());
        if (an == null || bn == null)
            return Task.FromResult(data.@this<number>.FromError(
                new global::app.error.ValidationError("math.max requires two numbers", "InvalidInput")));
        return Task.FromResult(number.Max(an, bn, policy));
    }
}
