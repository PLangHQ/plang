using App.Engine.Variables;

namespace App.modules.math;

[Action("min")]
public partial class Min : IContext
{
    public partial object A { get; init; }
    public partial object B { get; init; }

    public Task<Data> Run()
    {
        var result = Math.Min(MathHelper.ToDouble(A), MathHelper.ToDouble(B));
        return Task.FromResult(Data.Ok(MathHelper.PreserveType(result, A, B)));
    }
}
