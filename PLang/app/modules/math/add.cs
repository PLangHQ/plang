using app.variables;
using ExampleSpec = app.modules.Schema.Spec.Example;
using ActionSpec = app.modules.Schema.Spec.Action;

namespace app.modules.math;

[System.ComponentModel.Description("Add A and B together and return the numeric result")]
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

    public Task<data.@this> Run()
    {
        var result = MathHelper.ToDouble(A.Value) + MathHelper.ToDouble(B.Value);
        return Task.FromResult(Data(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
