using App.Variables;

namespace App.modules.math;

[Action("ceiling")]
public partial class Ceiling : IContext
{
    public partial object Value { get; init; }

    public Task<Data> Run()
    {
        var result = Math.Ceiling(MathHelper.ToDouble(Value));
        return Task.FromResult(Data.Ok(MathHelper.PreserveType(result, Value)));
    }
}
