using app.variables;
using number = global::app.types.number.@this;

namespace app.modules.math;

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
                new errors.ValidationError("math.round requires a number", "InvalidInput")));
        return Task.FromResult(number.Round(n, Decimals.Value));
    }
}
