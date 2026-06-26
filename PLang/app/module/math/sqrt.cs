using app.variable;
using number = global::app.type.number.@this;

namespace app.module.math;

[Action("sqrt")]
public partial class Sqrt : IContext
{
    public partial data.@this Value { get; init; }

    public async Task<data.@this<number>> Run()
    {
        var n = number.FromObject(await Value.Value());
        if (n == null)
            return Context.Error<number>(
                new global::app.error.ValidationError("math.sqrt requires a number", "InvalidInput"));
        // number.Sqrt surfaces negative input as ArithmeticError via Wrap.
        return number.Sqrt(n);
    }
}
