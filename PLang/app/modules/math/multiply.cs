using app.Variables;
using ExampleSpec = app.Modules.Schema.Spec.Example;
using ActionSpec = app.Modules.Schema.Spec.Action;

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
                new ActionSpec("variable", "set",      new() { ["Name"] = "%product%", ["Value"] = "%__data__%" }),
            }),
        new ExampleSpec(
            "set %area% = %width% * %height%",
            new[]
            {
                new ActionSpec("math",     "multiply", new() { ["A"] = "%width%", ["B"] = "%height%" }),
                new ActionSpec("variable", "set",      new() { ["Name"] = "%area%", ["Value"] = "%__data__%" }),
            }),
    };

    public partial Data.@this A { get; init; }
    public partial Data.@this B { get; init; }

    public Task<Data.@this> Run()
    {
        var result = MathHelper.ToDouble(A.Value) * MathHelper.ToDouble(B.Value);
        return Task.FromResult(Data(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
