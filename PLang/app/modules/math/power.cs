using app.variables;
using ExampleSpec = app.builder.Types.Spec.Example;
using ActionSpec = app.builder.Types.Spec.Action;
using number = global::app.types.number.@this;

using POverflow = global::app.types.number.OverflowMode;
using PPrecision = global::app.types.number.PrecisionMode;

namespace app.modules.math;

[Action("power")]
public partial class Power : IContext
{
    public static ExampleSpec[] ExamplesForLlm() => new[]
    {
        new ExampleSpec(
            "raise 2 to the power of 3, write to %pow%",
            new[]
            {
                new ActionSpec("math",     "power", new() { ["Base"] = 2, ["Exponent"] = 3 }),
                new ActionSpec("variable", "set",   new() { ["Name"] = "%pow%", ["Value"] = "%!data%" }),
            }),
        new ExampleSpec(
            "set %y% = %x% ^ 2",
            new[]
            {
                new ActionSpec("math",     "power", new() { ["Base"] = "%x%", ["Exponent"] = 2 }),
                new ActionSpec("variable", "set",   new() { ["Name"] = "%y%", ["Value"] = "%!data%" }),
            }),
    };

    public partial data.@this Base { get; init; }
    public partial data.@this Exponent { get; init; }
    public partial data.@this<POverflow>? Overflow { get; init; }
    public partial data.@this<PPrecision>? Precision { get; init; }

    public Task<data.@this<number>> Run()
    {
        var policy = MathPolicy.Resolve(Context, Overflow?.Value, Precision?.Value);
        var an = number.FromObject(Base.Value);
        var bn = number.FromObject(Exponent.Value);
        if (an == null || bn == null)
            return Task.FromResult(data.@this<number>.FromError(
                new errors.ValidationError("math.power requires base and exponent", "InvalidInput")));
        return Task.FromResult(number.Power(an, bn, policy));
    }
}
