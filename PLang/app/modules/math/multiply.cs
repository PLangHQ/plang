using app.variables;
using ExampleSpec = app.modules.Schema.Spec.Example;
using ActionSpec = app.modules.Schema.Spec.Action;

namespace app.modules.math;

[System.ComponentModel.Description("Multiply A by B and return the numeric result")]
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

    public Task<data.@this<object>> Run()
    {
        var result = MathHelper.ToDouble(A.Value) * MathHelper.ToDouble(B.Value);
        return Task.FromResult(data.@this<object>.Ok(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
