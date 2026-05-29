using app.variable;
using number = global::app.type.number.@this;

namespace app.module.math;

[Action("ceiling")]
public partial class Ceiling : IContext
{
    public partial data.@this Value { get; init; }

    public Task<data.@this<number>> Run()
    {
        var n = number.FromObject(Value.Value);
        if (n == null)
            return Task.FromResult(data.@this<number>.FromError(
                new global::app.error.ValidationError("math.ceiling requires a number", "InvalidInput")));
        return Task.FromResult(number.Ceiling(n));
    }
}
