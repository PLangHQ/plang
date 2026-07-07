using app.variable;
using ExampleSpec = app.type.spec.Example;
using ActionSpec = app.type.spec.Action;
using number = global::app.type.number.@this;


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
    /// <summary>Integer-overflow mode. Default: Promote (widen; never wrap).</summary>
    [Default(number.Overflow.Promote)]
    public partial data.@this<global::app.type.choice.@this<number.Overflow>> Overflow { get; init; }

    /// <summary>Precision mode for a double⊕decimal mix. Default: Error (require an explicit choice).</summary>
    [Default(number.Precision.Error)]
    public partial data.@this<global::app.type.choice.@this<number.Precision>> Precision { get; init; }

    [Code]
    public partial global::app.module.math.code.IMath Math { get; }

    public async Task<data.@this<number>> Run() => await Math.Power(this);
}
