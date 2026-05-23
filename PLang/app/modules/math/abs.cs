using app.variables;

namespace app.modules.math;

[ModuleDescription("Arithmetic operations on numeric values: add, subtract, multiply, divide, round, and more")]
[System.ComponentModel.Description("Return the absolute (non-negative) value of a number")]
[Action("abs")]
public partial class Abs : IContext
{
    public partial data.@this Value { get; init; }

    public Task<data.@this<object>> Run()
    {
        var result = Math.Abs(MathHelper.ToDouble(Value.Value));
        return Task.FromResult(data.@this<object>.Ok(MathHelper.PreserveType(result, Value.Value)));
    }
}
