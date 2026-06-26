using app.variable;
using number = global::app.type.number.@this;

namespace app.module.math;

[Action("floor")]
public partial class Floor : IContext
{
    public partial data.@this Value { get; init; }

    public async Task<data.@this<number>> Run()
    {
        var n = number.FromObject(await Value.Value());
        if (n == null)
            return Context.Error<number>(
                new global::app.error.ValidationError("math.floor requires a number", "InvalidInput"));
        return number.Floor(n);
    }
}
