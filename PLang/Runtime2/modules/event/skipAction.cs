using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.@event;

/// PLang: - skip action, value = %mockResponse%
/// PLang: - skip action, value = {"status": 200}
[Action("skipAction", Cacheable = false)]
public partial class SkipAction : IContext
{
    public partial object? Value { get; init; }

    public Task<Data> Run()
    {
        Context.EventOverride = Data.Ok(Value);
        return Task.FromResult(Data.Ok(Value));
    }
}
