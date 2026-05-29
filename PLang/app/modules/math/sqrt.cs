using app.variable;
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
                new global::app.error.ValidationError("math.sqrt requires a number", "InvalidInput")));
        // number.Sqrt surfaces negative input as ArithmeticError via Wrap.
        return Task.FromResult(number.Sqrt(n));
    }
}
