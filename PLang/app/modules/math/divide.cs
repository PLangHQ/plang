using app.variable;
using ExampleSpec = app.builder.Types.Spec.Example;
using ActionSpec = app.builder.Types.Spec.Action;
using number = global::app.type.number.@this;

using POverflow = global::app.type.number.OverflowMode;
using PPrecision = global::app.type.number.PrecisionMode;

namespace app.modules.math;

[Action("divide")]
public partial class Divide : IContext
{
    public static ExampleSpec[] ExamplesForLlm() => new[]
    {
        new ExampleSpec(
            "divide 10 by 4, write to %quotient%",
            new[]
            {
                new ActionSpec("math",     "divide", new() { ["A"] = 10, ["B"] = 4 }),
                new ActionSpec("variable", "set",    new() { ["Name"] = "%quotient%", ["Value"] = "%!data%" }),
            }),
        new ExampleSpec(
            "set %avg% = %total% / %count%",
            new[]
            {
                new ActionSpec("math",     "divide", new() { ["A"] = "%total%", ["B"] = "%count%" }),
                new ActionSpec("variable", "set",    new() { ["Name"] = "%avg%", ["Value"] = "%!data%" }),
            }),
    };

    public partial data.@this A { get; init; }
    public partial data.@this B { get; init; }
    public partial data.@this<POverflow>? Overflow { get; init; }
    public partial data.@this<PPrecision>? Precision { get; init; }

    // Divide leaves the integer track — 7/2 → 3.5. Truncating integer division
    // is the explicit math.intdiv action.
    public Task<data.@this<number>> Run()
    {
        var policy = MathPolicy.Resolve(Context, Overflow?.Value, Precision?.Value);
        var an = number.FromObject(A.Value);
        var bn = number.FromObject(B.Value);
        if (an == null || bn == null)
            return Task.FromResult(data.@this<number>.FromError(
                new global::app.error.ValidationError("math.divide requires two numbers", "InvalidInput")));
        return Task.FromResult(number.Divide(an, bn, policy));
    }
}
