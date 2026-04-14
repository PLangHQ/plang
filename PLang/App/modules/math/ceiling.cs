using App.Variables;

namespace App.modules.math;

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
