using app.variable;
using number = global::app.type.number.@this;

namespace app.module.math;

[Action("abs")]
public partial class Abs : IContext
{
    public partial data.@this Value { get; init; }

    public async Task<data.@this<number>> Run()
    {
        var n = number.FromObject(await Value.Value());
        if (n == null)
            return Context.Error<number>(
                new global::app.error.ValidationError("math.abs requires a number", "InvalidInput"));
        return number.Abs(n);
    }
}
