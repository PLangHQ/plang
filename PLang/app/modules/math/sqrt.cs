using app.variables;

namespace app.modules.math;

[Action("sqrt")]
public partial class Sqrt : IContext
{
    public partial data.@this Value { get; init; }

    public Task<data.@this<object>> Run()
    {
        var input = MathHelper.ToDouble(Value.Value);
        if (input < 0)
            return Task.FromResult(data.@this<object>.FromError(
                new app.errors.ValidationError("Cannot take square root of negative number", "InvalidInput")));

        var result = Math.Sqrt(input);
        return Task.FromResult(data.@this<object>.Ok(result));
    }
}
