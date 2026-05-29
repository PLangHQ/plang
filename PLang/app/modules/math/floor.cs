using app.variable;
using number = global::app.type.number.@this;

namespace app.modules.math;

[Action("floor")]
public partial class Floor : IContext
{
    public partial data.@this Value { get; init; }

    public Task<data.@this<number>> Run()
    {
        var n = number.FromObject(Value.Value);
        if (n == null)
            return Task.FromResult(data.@this<number>.FromError(
                new global::app.error.ValidationError("math.floor requires a number", "InvalidInput")));
        return Task.FromResult(number.Floor(n));
    }
}
