using app.variable;
using ExampleSpec = app.builder.type.Example;
using ActionSpec = app.builder.type.Action;
using number = global::app.type.number.@this;

using POverflow = global::app.type.number.OverflowMode;
using PPrecision = global::app.type.number.PrecisionMode;

namespace app.module.math;

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
    public partial data.@this<global::app.type.choice.@this<POverflow>>? Overflow { get; init; }
    public partial data.@this<global::app.type.choice.@this<PPrecision>>? Precision { get; init; }

    [Code]
    public partial global::app.module.math.code.IMath Math { get; }

    public async Task<data.@this<number>> Run() => await Math.Multiply(this);
}
