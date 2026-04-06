using App.Variables;

namespace App.modules.math;

[Action("abs")]
public partial class Abs : IContext
{
    public partial object Value { get; init; }

    public Task<Data.@this> Run()
    {
        var result = Math.Abs(MathHelper.ToDouble(Value));
        return Task.FromResult(App.Data.@this.Ok(MathHelper.PreserveType(result, Value)));
    }
}
