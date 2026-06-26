using app.variable;
using ExampleSpec = app.builder.type.Example;
using ActionSpec = app.builder.type.Action;
using number = global::app.type.number.@this;

using POverflow = global::app.type.number.OverflowMode;
using PPrecision = global::app.type.number.PrecisionMode;

namespace app.module.math;

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
    public partial data.@this<global::app.type.choice.@this<POverflow>>? Overflow { get; init; }
    public partial data.@this<global::app.type.choice.@this<PPrecision>>? Precision { get; init; }

    public async Task<data.@this<number>> Run()
    {
        var policy = MathPolicy.Resolve(Context, (Overflow == null ? null : await Overflow.Value())?.Value, (Precision == null ? null : await Precision.Value())?.Value);
        var an = number.FromObject(await A.Value());
        var bn = number.FromObject(await B.Value());
        if (an == null || bn == null)
            return Context.Error<number>(
                new global::app.error.ValidationError("math.intdiv requires two numbers", "InvalidInput"));
        return number.IntDivide(an, bn, policy);
    }
}
