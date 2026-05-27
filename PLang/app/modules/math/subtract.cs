using app.variables;
using ExampleSpec = app.builder.Types.Spec.Example;
using ActionSpec = app.builder.Types.Spec.Action;

namespace app.modules.math;

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

    public Task<data.@this<object>> Run()
    {
        var result = MathHelper.ToDouble(A.Value) - MathHelper.ToDouble(B.Value);
        return Task.FromResult(data.@this<object>.Ok(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
