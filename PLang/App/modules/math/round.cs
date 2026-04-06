using App.Engine.Variables;

namespace App.modules.math;

[Action("round")]
public partial class Round : IContext
{
    public partial object Value { get; init; }
    [Default(0)]
    public partial int Decimals { get; init; }

    public Task<Data> Run()
    {
        var result = Math.Round(MathHelper.ToDouble(Value), Decimals, MidpointRounding.AwayFromZero);
        if (Decimals == 0)
            return Task.FromResult(Data.Ok(MathHelper.PreserveType(result, Value)));
        return Task.FromResult(Data.Ok(result));
    }
}
