using app.variable;
using ExampleSpec = app.builder.Types.Spec.Example;
using ActionSpec = app.builder.Types.Spec.Action;
using number = global::app.type.number.@this;

using POverflow = global::app.type.number.OverflowMode;
using PPrecision = global::app.type.number.PrecisionMode;

namespace app.modules.math;

/// <summary>
/// Truncating integer division — the explicit opt-in for the C# semantics
/// the plain <c>math.divide</c> intentionally avoids. <c>7 intdiv 2 → 3</c>;
/// negative numerators truncate toward zero. Pairs with <c>math.modulo</c>.
/// </summary>
[Action("intdiv")]
public partial class IntDiv : IContext
{
    public static ExampleSpec[] ExamplesForLlm() => new[]
    {
        new ExampleSpec(
            "integer divide 7 by 2, write to %quotient%",
            new[]
            {
                new ActionSpec("math",     "intdiv", new() { ["A"] = 7, ["B"] = 2 }),
                new ActionSpec("variable", "set",    new() { ["Name"] = "%quotient%", ["Value"] = "%!data%" }),
            }),
    };

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
                new global::app.error.ValidationError("math.intdiv requires two numbers", "InvalidInput")));
        return Task.FromResult(number.IntDivide(an, bn, policy));
    }
}
