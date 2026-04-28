using App.Variables;
using static App.Catalog.ExampleHelpers;

namespace App.modules.math;

[System.ComponentModel.Description("Raise Base to the power of Exponent")]
[Action("power")]
public partial class Power : IContext
{
    public static App.Catalog.ExampleSpec[] ExamplesForLlm() => new[]
    {
        Example(
            "raise 2 to the power of 3, write to %pow%",
            Action("math.power",   new() { ["Base"] = 2, ["Exponent"] = 3 }),
            Action("variable.set", new() { ["Name"] = "%pow%", ["Value"] = "%__data__%" })
        ),
        Example(
            "set %y% = %x% ^ 2",
            Action("math.power",   new() { ["Base"] = "%x%", ["Exponent"] = 2 }),
            Action("variable.set", new() { ["Name"] = "%y%", ["Value"] = "%__data__%" })
        ),
    };

    public partial Data.@this Base { get; init; }
    public partial Data.@this Exponent { get; init; }

    public Task<Data.@this> Run()
    {
        var result = Math.Pow(MathHelper.ToDouble(Base.Value), MathHelper.ToDouble(Exponent.Value));
        return Task.FromResult(Data(MathHelper.PreserveType(result, Base.Value, Exponent.Value)));
    }
}
