using App.Variables;

namespace App.modules.math;

[System.ComponentModel.Description("Round Value to the specified number of Decimals (default 0, ties away from zero)")]
[Action("round")]
public partial class Round : IContext
{
    public partial Data.@this Value { get; init; }
    [Default(0)]
    public partial Data.@this<int> Decimals { get; init; }

    public Task<Data.@this> Run()
    {
        var result = Math.Round(MathHelper.ToDouble(Value.Value), Decimals.Value, MidpointRounding.AwayFromZero);
        if (Decimals.Value == 0)
            return Task.FromResult(Data(MathHelper.PreserveType(result, Value.Value)));
        return Task.FromResult(Data(result));
    }
}
