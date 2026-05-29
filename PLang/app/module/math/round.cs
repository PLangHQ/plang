using app.variable;
using number = global::app.type.number.@this;

namespace app.module.math;

[Action("round")]
public partial class Round : IContext
{
    public partial data.@this Value { get; init; }
    [Default(0)]
    public partial data.@this<int> Decimals { get; init; }

    public Task<data.@this<number>> Run()
    {
        var n = number.FromObject(Value.Value);
        if (n == null)
            return Task.FromResult(data.@this<number>.FromError(
                new global::app.error.ValidationError("math.round requires a number", "InvalidInput")));
        return Task.FromResult(number.Round(n, Decimals.Value));
    }
}
