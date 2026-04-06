using App.Variables;

namespace App.modules.math;

[Action("subtract")]
public partial class Subtract : IContext
{
    public partial object A { get; init; }
    public partial object B { get; init; }

    public Task<Data.@this> Run()
    {
        var result = MathHelper.ToDouble(A) - MathHelper.ToDouble(B);
        return Task.FromResult(App.Data.@this.Ok(MathHelper.PreserveType(result, A, B)));
    }
}
