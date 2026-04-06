using App.Variables;

namespace App.modules.math;

[Action("multiply")]
public partial class Multiply : IContext
{
    public partial object A { get; init; }
    public partial object B { get; init; }

    public Task<Data.@this> Run()
    {
        var result = MathHelper.ToDouble(A) * MathHelper.ToDouble(B);
        return Task.FromResult(Data(MathHelper.PreserveType(result, A, B)));
    }
}
