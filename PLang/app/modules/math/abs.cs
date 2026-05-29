using app.variables;
using number = global::app.types.number.@this;

namespace app.modules.math;

[Action("abs")]
public partial class Abs : IContext
{
    public partial data.@this Value { get; init; }

    public Task<data.@this<number>> Run()
    {
        var n = number.FromObject(Value.Value);
        if (n == null)
            return Task.FromResult(data.@this<number>.FromError(
                new global::app.error.ValidationError("math.abs requires a number", "InvalidInput")));
        return Task.FromResult(number.Abs(n));
    }
}
