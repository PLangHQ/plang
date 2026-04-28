using App.Variables;
using static App.Catalog.ExampleHelpers;

namespace App.modules.math;

[System.ComponentModel.Description("Subtract B from A and return the numeric result")]
[Action("subtract")]
public partial class Subtract : IContext
{
    public static App.Catalog.ExampleSpec[] ExamplesForLlm() => new[]
    {
        Example(
            "subtract 3 from 10, write to %diff%",
            Action("math.subtract", new() { ["A"] = 10, ["B"] = 3 }),
            Action("variable.set",  new() { ["Name"] = "%diff%", ["Value"] = "%__data__%" })
        ),
        Example(
            "set %total% = %total% - %discount%",
            Action("math.subtract", new() { ["A"] = "%total%", ["B"] = "%discount%" }),
            Action("variable.set",  new() { ["Name"] = "%total%", ["Value"] = "%__data__%" })
        ),
    };

    public partial Data.@this A { get; init; }
    public partial Data.@this B { get; init; }

    public Task<Data.@this> Run()
    {
        var result = MathHelper.ToDouble(A.Value) - MathHelper.ToDouble(B.Value);
        return Task.FromResult(Data(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
