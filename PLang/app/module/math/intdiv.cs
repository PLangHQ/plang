using app.variable;
using ExampleSpec = app.type.spec.Example;
using ActionSpec = app.type.spec.Action;
using number = global::app.type.number.@this;


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
    /// <summary>Integer-overflow mode. Default: Promote (widen; never wrap).</summary>
    [Default(number.Overflow.Promote)]
    public partial data.@this<global::app.type.item.choice.@this<number.Overflow>> Overflow { get; init; }

    /// <summary>Precision mode for a double⊕decimal mix. Default: Error (require an explicit choice).</summary>
    [Default(number.Precision.Error)]
    public partial data.@this<global::app.type.item.choice.@this<number.Precision>> Precision { get; init; }

    [Code]
    public partial global::app.module.math.code.IMath Math { get; }

    public async Task<data.@this<number>> Run() => await Math.IntDivide(this);
}
