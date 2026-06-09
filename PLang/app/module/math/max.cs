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

    public async Task<data.@this<number>> Run()
    {
        var policy = MathPolicy.Resolve(Context, (Overflow == null ? null : await Overflow.Value())?.Value, (Precision == null ? null : await Precision.Value())?.Value);
        var an = number.FromObject(await A.Value());
        var bn = number.FromObject(await B.Value());
        if (an == null || bn == null)
            return data.@this<number>.FromError(
                new global::app.error.ValidationError("math.max requires two numbers", "InvalidInput"));
        return number.Max(an, bn, policy);
    }
}
