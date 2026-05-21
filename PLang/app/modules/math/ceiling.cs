using app.variables;

namespace app.modules.math;

[System.ComponentModel.Description("Return the smallest integer greater than or equal to Value (ceiling)")]
[Action("ceiling")]
public partial class Ceiling : IContext
{
    public partial data.@this Value { get; init; }

    public Task<data.@this> Run()
    {
        var result = Math.Ceiling(MathHelper.ToDouble(Value.Value));
        return Task.FromResult(Data(MathHelper.PreserveType(result, Value.Value)));
    }
}
