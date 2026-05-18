using app.variables;
using ExampleSpec = app.modules.Schema.Spec.Example;
using ActionSpec = app.modules.Schema.Spec.Action;

namespace app.modules.math;

[System.ComponentModel.Description("Subtract B from A and return the numeric result")]
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
                new ActionSpec("variable", "set",      new() { ["Name"] = "%diff%", ["Value"] = "%__data__%" }),
            }),
        new ExampleSpec(
            "set %total% = %total% - %discount%",
            new[]
            {
                new ActionSpec("math",     "subtract", new() { ["A"] = "%total%", ["B"] = "%discount%" }),
                new ActionSpec("variable", "set",      new() { ["Name"] = "%total%", ["Value"] = "%__data__%" }),
            }),
    };

    public partial data.@this A { get; init; }
    public partial data.@this B { get; init; }

    public Task<data.@this> Run()
    {
        var result = MathHelper.ToDouble(A.Value) - MathHelper.ToDouble(B.Value);
        return Task.FromResult(Data(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
