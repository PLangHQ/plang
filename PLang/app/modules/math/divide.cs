using app.variables;
using ExampleSpec = app.modules.Schema.Spec.Example;
using ActionSpec = app.modules.Schema.Spec.Action;

namespace app.modules.math;

[System.ComponentModel.Description("Divide A by B and return the result; fails on division by zero")]
[Action("divide")]
public partial class Divide : IContext
{
    public static ExampleSpec[] ExamplesForLlm() => new[]
    {
        new ExampleSpec(
            "divide 10 by 4, write to %quotient%",
            new[]
            {
                new ActionSpec("math",     "divide", new() { ["A"] = 10, ["B"] = 4 }),
                new ActionSpec("variable", "set",    new() { ["Name"] = "%quotient%", ["Value"] = "%__data__%" }),
            }),
        new ExampleSpec(
            "set %avg% = %total% / %count%",
            new[]
            {
                new ActionSpec("math",     "divide", new() { ["A"] = "%total%", ["B"] = "%count%" }),
                new ActionSpec("variable", "set",    new() { ["Name"] = "%avg%", ["Value"] = "%__data__%" }),
            }),
    };

    public partial data.@this A { get; init; }
    public partial data.@this B { get; init; }

    public Task<data.@this> Run()
    {
        var divisor = MathHelper.ToDouble(B.Value);
        if (divisor == 0)
            return Task.FromResult(Error(
                new app.errors.ValidationError("Division by zero", "DivisionByZero")));

        var result = MathHelper.ToDouble(A.Value) / divisor;
        return Task.FromResult(Data(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
