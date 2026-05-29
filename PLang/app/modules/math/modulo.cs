using app.variable;

using POverflow = global::app.types.number.OverflowMode;
using number = global::app.types.number.@this;
using PPrecision = global::app.types.number.PrecisionMode;

namespace app.modules.math;

[Action("modulo")]
public partial class Modulo : IContext
{
    public partial data.@this A { get; init; }
    public partial data.@this B { get; init; }
    public partial data.@this<POverflow>? Overflow { get; init; }
    public partial data.@this<PPrecision>? Precision { get; init; }

    public Task<data.@this<number>> Run()
    {
        var policy = MathPolicy.Resolve(Context, Overflow?.Value, Precision?.Value);
        var an = number.FromObject(A.Value);
        var bn = number.FromObject(B.Value);
        if (an == null || bn == null)
            return Task.FromResult(data.@this<number>.FromError(
                new global::app.error.ValidationError("math.modulo requires two numbers", "InvalidInput")));
        return Task.FromResult(number.Modulo(an, bn, policy));
    }
}
