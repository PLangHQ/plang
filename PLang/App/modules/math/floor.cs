using App.Variables;

namespace App.modules.math;

[System.ComponentModel.Description("Return the largest integer less than or equal to Value (floor)")]
[Action("floor")]
public partial class Floor : IContext
{
    public partial Data.@this Value { get; init; }

    public Task<Data.@this> Run()
    {
        var result = Math.Floor(MathHelper.ToDouble(Value.Value));
        return Task.FromResult(Data(MathHelper.PreserveType(result, Value.Value)));
    }
}
