using App.Variables;
using static App.Catalog.ExampleHelpers;

namespace App.modules.math;

[System.ComponentModel.Description("Divide A by B and return the result; fails on division by zero")]
[Action("divide")]
public partial class Divide : IContext
{
    public static App.Catalog.ExampleSpec[] ExamplesForLlm() => new[]
    {
        Example(
            "divide 10 by 4, write to %quotient%",
            Action("math.divide",  new() { ["A"] = 10, ["B"] = 4 }),
            Action("variable.set", new() { ["Name"] = "%quotient%", ["Value"] = "%__data__%" })
        ),
        Example(
            "set %avg% = %total% / %count%",
            Action("math.divide",  new() { ["A"] = "%total%", ["B"] = "%count%" }),
            Action("variable.set", new() { ["Name"] = "%avg%", ["Value"] = "%__data__%" })
        ),
    };

    public partial Data.@this A { get; init; }
    public partial Data.@this B { get; init; }

    public Task<Data.@this> Run()
    {
        var divisor = MathHelper.ToDouble(B.Value);
        if (divisor == 0)
            return Task.FromResult(Error(
                new App.Errors.ValidationError("Division by zero", "DivisionByZero")));

        var result = MathHelper.ToDouble(A.Value) / divisor;
        return Task.FromResult(Data(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
