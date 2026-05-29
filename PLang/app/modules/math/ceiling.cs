using app.variables;
using number = global::app.types.number.@this;

namespace app.modules.math;

[Action("ceiling")]
public partial class Ceiling : IContext
{
    public partial data.@this Value { get; init; }

    public Task<data.@this<number>> Run()
    {
        var n = number.FromObject(Value.Value);
        if (n == null)
            return Task.FromResult(data.@this<number>.FromError(
                new errors.ValidationError("math.ceiling requires a number", "InvalidInput")));
        return Task.FromResult(number.Ceiling(n));
    }
}
