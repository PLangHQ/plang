using app.variables;
using number = global::app.types.number.@this;

namespace app.modules.math;

[Action("sqrt")]
public partial class Sqrt : IContext
{
    public partial data.@this Value { get; init; }

    public Task<data.@this<number>> Run()
    {
        var n = number.FromObject(Value.Value);
        if (n == null)
            return Task.FromResult(data.@this<number>.FromError(
                new errors.ValidationError("math.sqrt requires a number", "InvalidInput")));
        if (n.ToDouble() < 0)
            return Task.FromResult(data.@this<number>.FromError(
                new errors.ValidationError("Cannot take square root of negative number", "InvalidInput")));
        return Task.FromResult(number.Sqrt(n));
    }
}
