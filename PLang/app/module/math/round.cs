using app.variable;
using number = global::app.type.number.@this;

namespace app.module.math;

[Action("round")]
public partial class Round : IContext
{
    public partial data.@this Value { get; init; }
    [Default(0)]
    public partial data.@this<global::app.type.number.@this> Decimals { get; init; }

    public async Task<data.@this<number>> Run()
    {
        var n = number.FromObject(await Value.Value());
        if (n == null)
            return data.@this<number>.FromError(
                new global::app.error.ValidationError("math.round requires a number", "InvalidInput"));
        return number.Round(n, (await Decimals.Value())!);
    }
}
