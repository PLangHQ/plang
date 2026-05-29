using app.variables;

using POverflow = global::app.types.number.OverflowMode;
using Number = global::app.types.number.@this;
using PPrecision = global::app.types.number.PrecisionMode;

namespace app.modules.math;

[Action("modulo")]
public partial class Modulo : IContext
{
    public partial data.@this A { get; init; }
    public partial data.@this B { get; init; }
    public partial data.@this<POverflow>? Overflow { get; init; }
    public partial data.@this<PPrecision>? Precision { get; init; }

    public Task<data.@this<Number>> Run()
    {
        var policy = MathPolicy.Resolve(Context, Overflow?.Value, Precision?.Value);
        var an = Number.FromObject(A.Value);
        var bn = Number.FromObject(B.Value);
        if (an == null || bn == null)
            return Task.FromResult(data.@this<Number>.FromError(
                new errors.ValidationError("math.modulo requires two numbers", "InvalidInput")));
        return Task.FromResult(Number.Modulo(an, bn, policy));
    }
}
