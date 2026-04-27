using App.Variables;

namespace App.modules.math;

[System.ComponentModel.Description("Raise Base to the power of Exponent")]
[Action("power")]
public partial class Power : IContext
{
    public partial Data.@this Base { get; init; }
    public partial Data.@this Exponent { get; init; }

    public Task<Data.@this> Run()
    {
        var result = Math.Pow(MathHelper.ToDouble(Base.Value), MathHelper.ToDouble(Exponent.Value));
        return Task.FromResult(Data(MathHelper.PreserveType(result, Base.Value, Exponent.Value)));
    }
}
