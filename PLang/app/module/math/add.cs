using app.variable;
using ExampleSpec = app.builder.type.Example;
using ActionSpec = app.builder.type.Action;
using number = global::app.type.number.@this;

using POverflow = global::app.type.number.OverflowMode;
using PPrecision = global::app.type.number.PrecisionMode;

namespace app.module.math;

[Action("add")]
public partial class Add : IContext
{
    public static ExampleSpec[] ExamplesForLlm() => new[]
    {
        new ExampleSpec(
            "add 5 and 3, write to %sum%",
            new[]
            {
                new ActionSpec("math",     "add", new() { ["A"] = 5, ["B"] = 3 }),
                new ActionSpec("variable", "set", new() { ["Name"] = "%sum%", ["Value"] = "%!data%" }),
            }),
        new ExampleSpec(
            "set %count% = %count% + 1",
            new[]
            {
                new ActionSpec("math",     "add", new() { ["A"] = "%count%", ["B"] = 1 }),
                new ActionSpec("variable", "set", new() { ["Name"] = "%count%", ["Value"] = "%!data%" }),
            }),
    };

    public partial data.@this A { get; init; }
    public partial data.@this B { get; init; }

    /// <summary>Per-step overflow override; nullable IS the optional marker.</summary>
    public partial data.@this<global::app.type.choice.@this<POverflow>>? Overflow { get; init; }

    /// <summary>Per-step precision-mix override; nullable IS the optional marker.</summary>
    public partial data.@this<global::app.type.choice.@this<PPrecision>>? Precision { get; init; }

    public Task<data.@this<number>> Run()
    {
        var policy = MathPolicy.Resolve(Context,
            (Overflow?.Materialize() as global::app.type.choice.@this<POverflow>)?.Value, (Precision?.Materialize() as global::app.type.choice.@this<PPrecision>)?.Value);
        var an = number.FromObject(A.Materialize());
        var bn = number.FromObject(B.Materialize());
        if (an == null || bn == null)
            return Task.FromResult(data.@this<number>.FromError(
                new global::app.error.ValidationError("math.add requires two numbers", "InvalidInput")));
        return Task.FromResult(number.Add(an, bn, policy));
    }
}
