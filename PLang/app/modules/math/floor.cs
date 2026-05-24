using app.variables;

namespace app.modules.math;

[Action("floor")]
public partial class Floor : IContext
{
    public partial data.@this Value { get; init; }

    public Task<data.@this<object>> Run()
    {
        var result = Math.Floor(MathHelper.ToDouble(Value.Value));
        return Task.FromResult(data.@this<object>.Ok(MathHelper.PreserveType(result, Value.Value)));
    }
}
