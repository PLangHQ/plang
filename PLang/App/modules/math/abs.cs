using App.Variables;

namespace App.modules.math;

[Action("abs")]
public partial class Abs : IContext
{
    public partial Data.@this Value { get; init; }

    public Task<Data.@this> Run()
    {
        var result = Math.Abs(MathHelper.ToDouble(Value.Value));
        return Task.FromResult(Data(MathHelper.PreserveType(result, Value.Value)));
    }
}
