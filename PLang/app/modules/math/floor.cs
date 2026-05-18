using app.variables;

namespace app.modules.math;

[System.ComponentModel.Description("Return the largest integer less than or equal to Value (floor)")]
[Action("floor")]
public partial class Floor : IContext
{
    public partial data.@this Value { get; init; }

    public Task<data.@this> Run()
    {
        var result = Math.Floor(MathHelper.ToDouble(Value.Value));
        return Task.FromResult(Data(MathHelper.PreserveType(result, Value.Value)));
    }
}
