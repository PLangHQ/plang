using App.Variables;

namespace App.modules.math;

[Action("round")]
public partial class Round : IContext
{
    public partial object Value { get; init; }
    [Default(0)]
    public partial int Decimals { get; init; }

    public Task<Data.@this> Run()
    {
        var result = Math.Round(MathHelper.ToDouble(Value), Decimals, MidpointRounding.AwayFromZero);
        if (Decimals == 0)
            return Task.FromResult(Data.@this.Ok(MathHelper.PreserveType(result, Value)));
        return Task.FromResult(Data.@this.Ok(result));
    }
}
