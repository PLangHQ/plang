using App.Variables;

namespace App.modules.@event;

/// <summary>
/// Skips the current action and returns a custom value instead.
/// Use inside a beforeAction event handler to prevent the real action from running.
/// Sets context.EventOverride so the action runner returns this value.
/// </summary>
[Example("skip action, value = %mockResponse%", "Value=%mockResponse%")]
[Example("skip action, value = {\"status\": 200}", "Value={\"status\": 200}")]
[Action("skipAction", Cacheable = false)]
public partial class SkipAction : IContext
{
    /// <summary>Value to return instead of the action's real result. Null returns empty success.</summary>
    public partial object? Value { get; init; }

    public Task<Data> Run()
    {
        Context.EventOverride = Data.Ok(Value);
        return Task.FromResult(Data.Ok(Value));
    }
}
