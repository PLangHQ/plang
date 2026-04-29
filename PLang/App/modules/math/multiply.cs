using App.Variables;
using static App.Catalog.ExampleHelpers;

namespace App.modules.math;

[System.ComponentModel.Description("Multiply A by B and return the numeric result")]
[Action("multiply")]
public partial class Multiply : IContext
{
    public static App.Catalog.ExampleSpec[] ExamplesForLlm() => new[]
    {
        Example(
            "multiply 6 by 7, write to %product%",
            Action("math.multiply", new() { ["A"] = 6, ["B"] = 7 }),
            Action("variable.set",  new() { ["Name"] = "%product%", ["Value"] = "%__data__%" })
        ),
        Example(
            "set %area% = %width% * %height%",
            Action("math.multiply", new() { ["A"] = "%width%", ["B"] = "%height%" }),
            Action("variable.set",  new() { ["Name"] = "%area%", ["Value"] = "%__data__%" })
        ),
    };

    public partial Data.@this A { get; init; }
    public partial Data.@this B { get; init; }

    public Task<Data.@this> Run()
    {
        var result = MathHelper.ToDouble(A.Value) * MathHelper.ToDouble(B.Value);
        return Task.FromResult(Data(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
