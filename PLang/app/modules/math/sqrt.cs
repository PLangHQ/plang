using app.variables;

namespace app.modules.math;

[System.ComponentModel.Description("Return the square root of Value; fails for negative inputs")]
[Action("sqrt")]
public partial class Sqrt : IContext
{
    public partial data.@this Value { get; init; }

    public Task<data.@this> Run()
    {
        var input = MathHelper.ToDouble(Value.Value);
        if (input < 0)
            return Task.FromResult(Error(
                new app.errors.ValidationError("Cannot take square root of negative number", "InvalidInput")));

        var result = Math.Sqrt(input);
        return Task.FromResult(Data(result));
    }
}
