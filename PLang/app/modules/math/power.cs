using app.variables;
using ExampleSpec = app.modules.Schema.Spec.Example;
using ActionSpec = app.modules.Schema.Spec.Action;

namespace app.modules.math;

[System.ComponentModel.Description("Raise Base to the power of Exponent")]
[Action("power")]
public partial class Power : IContext
{
    public static ExampleSpec[] ExamplesForLlm() => new[]
    {
        new ExampleSpec(
            "raise 2 to the power of 3, write to %pow%",
            new[]
            {
                new ActionSpec("math",     "power", new() { ["Base"] = 2, ["Exponent"] = 3 }),
                new ActionSpec("variable", "set",   new() { ["Name"] = "%pow%", ["Value"] = "%__data__%" }),
            }),
        new ExampleSpec(
            "set %y% = %x% ^ 2",
            new[]
            {
                new ActionSpec("math",     "power", new() { ["Base"] = "%x%", ["Exponent"] = 2 }),
                new ActionSpec("variable", "set",   new() { ["Name"] = "%y%", ["Value"] = "%__data__%" }),
            }),
    };

    public partial data.@this Base { get; init; }
    public partial data.@this Exponent { get; init; }

    public Task<data.@this> Run()
    {
        var result = Math.Pow(MathHelper.ToDouble(Base.Value), MathHelper.ToDouble(Exponent.Value));
        return Task.FromResult(Data(MathHelper.PreserveType(result, Base.Value, Exponent.Value)));
    }
}
