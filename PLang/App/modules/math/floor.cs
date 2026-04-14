using App.Variables;

namespace App.modules.math;

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
