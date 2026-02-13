using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.math;

[Action("subtract")]
public partial class Subtract : IContext
{
    public partial object A { get; init; }
    public partial object B { get; init; }

    public Task<Data> Run()
    {
        var result = MathHelper.ToDouble(A) - MathHelper.ToDouble(B);
        return Task.FromResult(Data.Ok(MathHelper.PreserveType(result, A, B)));
    }
}
