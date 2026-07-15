using app.variable;
using ExampleSpec = app.type.spec.Example;
using ActionSpec = app.type.spec.Action;
using number = global::app.type.item.number.@this;


namespace app.module.action.math;

[Action("multiply")]
public partial class Multiply : IContext
{
    public static ExampleSpec[] ExamplesForLlm() => new[]
    {
        new ExampleSpec(
            "multiply 6 by 7, write to %product%",
            new[]
            {
                new ActionSpec("math",     "multiply", new() { ["A"] = 6, ["B"] = 7 }),
                new ActionSpec("variable", "set",      new() { ["Name"] = "%product%", ["Value"] = "%!data%" }),
            }),
        new ExampleSpec(
            "set %area% = %width% * %height%",
            new[]
            {
                new ActionSpec("math",     "multiply", new() { ["A"] = "%width%", ["B"] = "%height%" }),
                new ActionSpec("variable", "set",      new() { ["Name"] = "%area%", ["Value"] = "%!data%" }),
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
    public partial global::app.module.action.math.code.IMath Math { get; }

    public async Task<data.@this<number>> Run() => await Math.Multiply(this);
}
