using app.variables;

namespace app.modules.math;

[Action("round")]
public partial class Round : IContext
{
    public partial data.@this Value { get; init; }
    [Default(0)]
    public partial data.@this<int> Decimals { get; init; }

    public Task<data.@this<object>> Run()
    {
        var result = Math.Round(MathHelper.ToDouble(Value.Value), Decimals.Value, MidpointRounding.AwayFromZero);
        if (Decimals.Value == 0)
            return Task.FromResult(data.@this<object>.Ok(MathHelper.PreserveType(result, Value.Value)));
        return Task.FromResult(data.@this<object>.Ok(result));
    }
}
