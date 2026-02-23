using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.math;

[Action("max")]
public partial class Max : IContext
{
    public partial object A { get; init; }
    public partial object B { get; init; }

    public Task<Data> Run()
    {
        var result = Math.Max(MathHelper.ToDouble(A), MathHelper.ToDouble(B));
        return Task.FromResult(Data.Ok(MathHelper.PreserveType(result, A, B)));
    }
}
