using App.Variables;
using ExampleSpec = App.Modules.Schema.Spec.Example;
using ActionSpec = App.Modules.Schema.Spec.Action;

namespace App.modules.math;

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

    public partial Data.@this A { get; init; }
    public partial Data.@this B { get; init; }

    public Task<Data.@this> Run()
    {
        var result = MathHelper.ToDouble(A.Value) + MathHelper.ToDouble(B.Value);
        return Task.FromResult(Data(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
