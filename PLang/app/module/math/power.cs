using app.variable;
using ExampleSpec = app.builder.type.Example;
using ActionSpec = app.builder.type.Action;
using number = global::app.type.number.@this;

using POverflow = global::app.type.number.OverflowMode;
using PPrecision = global::app.type.number.PrecisionMode;

namespace app.module.math;

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
    public partial data.@this<global::app.type.choice.@this<POverflow>>? Overflow { get; init; }
    public partial data.@this<global::app.type.choice.@this<PPrecision>>? Precision { get; init; }

    public async Task<data.@this<number>> Run()
    {
        var policy = MathPolicy.Resolve(Context, (Overflow == null ? null : await Overflow.Value())?.Value, (Precision == null ? null : await Precision.Value())?.Value);
        var an = number.FromObject(await Base.Value());
        var bn = number.FromObject(await Exponent.Value());
        if (an == null || bn == null)
            return Context.Error<number>(
                new global::app.error.ValidationError("math.power requires base and exponent", "InvalidInput"));
        return number.Power(an, bn, policy);
    }
}
