using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.math;

[Action("add")]
public partial class Add : IContext
{
    public partial object A { get; init; }
    public partial object B { get; init; }

    public Task<Data> Run()
    {
        var result = MathHelper.ToDouble(A) + MathHelper.ToDouble(B);
        return Task.FromResult(Data.Ok(MathHelper.PreserveType(result, A, B)));
    }
}
