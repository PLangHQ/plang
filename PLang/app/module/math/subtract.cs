using app.variable;
using ExampleSpec = app.type.spec.Example;
using ActionSpec = app.type.spec.Action;
using number = global::app.type.item.number.@this;


namespace app.module.math;

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
    /// <summary>Integer-overflow mode. Default: Promote (widen; never wrap).</summary>
    [Default(number.Overflow.Promote)]
    public partial data.@this<global::app.type.item.choice.@this<number.Overflow>> Overflow { get; init; }

    /// <summary>Precision mode for a double⊕decimal mix. Default: Error (require an explicit choice).</summary>
    [Default(number.Precision.Error)]
    public partial data.@this<global::app.type.item.choice.@this<number.Precision>> Precision { get; init; }

    [Code]
    public partial global::app.module.math.code.IMath Math { get; }

    public async Task<data.@this<number>> Run() => await Math.Subtract(this);
}
