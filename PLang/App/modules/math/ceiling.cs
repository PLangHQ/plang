using App.Variables;

namespace App.modules.math;

[System.ComponentModel.Description("Return the smallest integer greater than or equal to Value (ceiling)")]
[Action("ceiling")]
public partial class Ceiling : IContext
{
    public partial Data.@this Value { get; init; }

    public Task<Data.@this> Run()
    {
        var result = Math.Ceiling(MathHelper.ToDouble(Value.Value));
        return Task.FromResult(Data(MathHelper.PreserveType(result, Value.Value)));
    }
}
