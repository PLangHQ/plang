using app.variable;
using ExampleSpec = app.builder.type.Example;
using ActionSpec = app.builder.type.Action;
using number = global::app.type.number.@this;

using POverflow = global::app.type.number.OverflowMode;
using PPrecision = global::app.type.number.PrecisionMode;

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
    public partial data.@this<global::app.type.choice.@this<POverflow>>? Overflow { get; init; }
    public partial data.@this<global::app.type.choice.@this<PPrecision>>? Precision { get; init; }

    public async Task<data.@this<number>> Run()
    {
        var policy = MathPolicy.Resolve(Context, (Overflow == null ? null : await Overflow.Value())?.Value, (Precision == null ? null : await Precision.Value())?.Value);
        var an = number.FromObject(await A.Value());
        var bn = number.FromObject(await B.Value());
        if (an == null || bn == null)
            return Context.Error<number>(
                new global::app.error.ValidationError("math.subtract requires two numbers", "InvalidInput"));
        return number.Subtract(an, bn, policy);
    }
}
