using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.math;

[Action("floor")]
public partial class Floor : IContext
{
    public partial object Value { get; init; }

    public Task<Data> Run()
    {
        var result = Math.Floor(MathHelper.ToDouble(Value));
        return Task.FromResult(Data.Ok(MathHelper.PreserveType(result, Value)));
    }
}
