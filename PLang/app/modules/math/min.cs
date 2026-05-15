using app.Variables;

namespace app.modules.math;

[System.ComponentModel.Description("Return the smaller of A and B")]
[Action("min")]
public partial class Min : IContext
{
    public partial data.@this A { get; init; }
    public partial data.@this B { get; init; }

    public Task<data.@this> Run()
    {
        var result = Math.Min(MathHelper.ToDouble(A.Value), MathHelper.ToDouble(B.Value));
        return Task.FromResult(Data(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
