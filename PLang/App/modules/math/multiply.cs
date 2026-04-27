using App.Variables;

namespace App.modules.math;

[System.ComponentModel.Description("Multiply A by B and return the numeric result")]
[Action("multiply")]
public partial class Multiply : IContext
{
    public partial Data.@this A { get; init; }
    public partial Data.@this B { get; init; }

    public Task<Data.@this> Run()
    {
        var result = MathHelper.ToDouble(A.Value) * MathHelper.ToDouble(B.Value);
        return Task.FromResult(Data(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
