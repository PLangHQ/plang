using App.Variables;

namespace App.modules.math;

[System.ComponentModel.Description("Add A and B together and return the numeric result")]
[Action("add")]
public partial class Add : IContext
{
    public partial Data.@this A { get; init; }
    public partial Data.@this B { get; init; }

    public Task<Data.@this> Run()
    {
        var result = MathHelper.ToDouble(A.Value) + MathHelper.ToDouble(B.Value);
        return Task.FromResult(Data(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
