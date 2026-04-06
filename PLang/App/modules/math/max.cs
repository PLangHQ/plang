using App.Variables;

namespace App.modules.math;

[Action("max")]
public partial class Max : IContext
{
    public partial object A { get; init; }
    public partial object B { get; init; }

    public Task<Data.@this> Run()
    {
        var result = Math.Max(MathHelper.ToDouble(A), MathHelper.ToDouble(B));
        return Task.FromResult(App.Data.@this.Ok(MathHelper.PreserveType(result, A, B)));
    }
}
