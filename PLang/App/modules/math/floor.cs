using App.Variables;

namespace App.modules.math;

[Action("floor")]
public partial class Floor : IContext
{
    public partial object Value { get; init; }

    public Task<Data.@this> Run()
    {
        var result = Math.Floor(MathHelper.ToDouble(Value));
        return Task.FromResult(App.Data.@this.Ok(MathHelper.PreserveType(result, Value)));
    }
}
