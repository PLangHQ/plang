using App.Variables;

namespace App.modules.math;

[System.ComponentModel.Description("Return the smaller of A and B")]
[Action("min")]
public partial class Min : IContext
{
    public partial Data.@this A { get; init; }
    public partial Data.@this B { get; init; }

    public Task<Data.@this> Run()
    {
        var result = Math.Min(MathHelper.ToDouble(A.Value), MathHelper.ToDouble(B.Value));
        return Task.FromResult(Data(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
