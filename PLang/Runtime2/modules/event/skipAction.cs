using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.@event;

[Example("skip action, value = %mockResponse%", "Value=%mockResponse%")]
[Example("skip action, value = {\"status\": 200}", "Value={\"status\": 200}")]
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
