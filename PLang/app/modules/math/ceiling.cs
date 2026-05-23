using app.variables;

namespace app.modules.math;

[Action("ceiling")]
public partial class Ceiling : IContext
{
    public partial data.@this Value { get; init; }

    public Task<data.@this<object>> Run()
    {
        var result = Math.Ceiling(MathHelper.ToDouble(Value.Value));
        return Task.FromResult(data.@this<object>.Ok(MathHelper.PreserveType(result, Value.Value)));
    }
}
