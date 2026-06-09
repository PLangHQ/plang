using app.variable;

namespace app.module.@event;

/// <summary>
/// Skips the current action and returns a custom value instead.
/// Use inside a beforeAction event handler to prevent the real action from running.
/// Sets context.EventOverride so the action runner returns this value.
/// </summary>
[Action("skipAction", Cacheable = false)]
public partial class SkipAction : IContext
{
    /// <summary>Value to return instead of the action's real result. Null returns empty success.</summary>
    public partial data.@this Value { get; init; }

    public Task<data.@this> Run()
    {
        Context.EventOverride = Data(Value?.Materialize());
        return Task.FromResult(Data(Value?.Materialize()));
    }
}
