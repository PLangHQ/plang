using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.math;

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
