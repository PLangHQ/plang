using app.variables;
using ExampleSpec = app.builder.Types.Spec.Example;
using ActionSpec = app.builder.Types.Spec.Action;
using Number = global::app.types.number.@this;

using POverflow = global::app.types.number.OverflowMode;
using PPrecision = global::app.types.number.PrecisionMode;

namespace app.modules.math;

[Action("subtract")]
public partial class Subtract : IContext
{
    public static ExampleSpec[] ExamplesForLlm() => new[]
    {
        new ExampleSpec(
            "subtract 3 from 10, write to %diff%",
            new[]
            {
                new ActionSpec("math",     "subtract", new() { ["A"] = 10, ["B"] = 3 }),
                new ActionSpec("variable", "set",      new() { ["Name"] = "%diff%", ["Value"] = "%!data%" }),
            }),
        new ExampleSpec(
            "set %total% = %total% - %discount%",
            new[]
            {
                new ActionSpec("math",     "subtract", new() { ["A"] = "%total%", ["B"] = "%discount%" }),
                new ActionSpec("variable", "set",      new() { ["Name"] = "%total%", ["Value"] = "%!data%" }),
            }),
    };

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
                new errors.ValidationError("math.subtract requires two numbers", "InvalidInput")));
        return Task.FromResult(Number.Subtract(an, bn, policy));
    }
}
