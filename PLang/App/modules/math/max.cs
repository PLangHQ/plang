using App.Variables;

namespace App.modules.math;

[System.ComponentModel.Description("Return the larger of A and B")]
[Action("max")]
public partial class Max : IContext
{
    public partial Data.@this A { get; init; }
    public partial Data.@this B { get; init; }

    public Task<Data.@this> Run()
    {
        var result = Math.Max(MathHelper.ToDouble(A.Value), MathHelper.ToDouble(B.Value));
        return Task.FromResult(Data(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
