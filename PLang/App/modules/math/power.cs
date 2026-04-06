using App.Engine.Variables;

namespace App.modules.math;

[Action("power")]
public partial class Power : IContext
{
    public partial object Base { get; init; }
    public partial object Exponent { get; init; }

    public Task<Data> Run()
    {
        var result = Math.Pow(MathHelper.ToDouble(Base), MathHelper.ToDouble(Exponent));
        return Task.FromResult(Data.Ok(MathHelper.PreserveType(result, Base, Exponent)));
    }
}
