using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.math;

[Action("abs")]
public partial class Abs : IContext
{
    public partial object Value { get; init; }

    public Task<Data> Run()
    {
        var result = Math.Abs(MathHelper.ToDouble(Value));
        return Task.FromResult(Data.Ok(MathHelper.PreserveType(result, Value)));
    }
}
