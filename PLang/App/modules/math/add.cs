using App.Variables;
using static App.Catalog.ExampleHelpers;

namespace App.modules.math;

[System.ComponentModel.Description("Add A and B together and return the numeric result")]
[Action("add")]
public partial class Add : IContext
{
    public static App.Catalog.ExampleSpec[] ExamplesForLlm() => new[]
    {
        Example(
            "add 5 and 3, write to %sum%",
            Action("math.add",     new() { ["A"] = 5, ["B"] = 3 }),
            Action("variable.set", new() { ["Name"] = "%sum%", ["Value"] = "%__data__%" })
        ),
        Example(
            "set %count% = %count% + 1",
            Action("math.add",     new() { ["A"] = "%count%", ["B"] = 1 }),
            Action("variable.set", new() { ["Name"] = "%count%", ["Value"] = "%__data__%" })
        ),
    };

    public partial Data.@this A { get; init; }
    public partial Data.@this B { get; init; }

    public Task<Data.@this> Run()
    {
        var result = MathHelper.ToDouble(A.Value) + MathHelper.ToDouble(B.Value);
        return Task.FromResult(Data(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
