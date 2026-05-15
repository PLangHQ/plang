using app.Variables;

namespace app.modules.@event;

/// <summary>
/// Skips the current action and returns a custom value instead.
/// Use inside a beforeAction event handler to prevent the real action from running.
/// Sets context.EventOverride so the action runner returns this value.
/// </summary>
[System.ComponentModel.Description("Skip the intercepted action and return a custom value from inside a beforeAction event handler")]
[Example("skip action, value = %mockResponse%",
    "event.skipAction Value([object] %mockResponse%)")]
[Action("skipAction", Cacheable = false)]
public partial class SkipAction : IContext
{
    /// <summary>Value to return instead of the action's real result. Null returns empty success.</summary>
    public partial Data.@this Value { get; init; }

    public Task<Data.@this> Run()
    {
        Context.EventOverride = Data(Value?.Value);
        return Task.FromResult(Data(Value?.Value));
    }
}
