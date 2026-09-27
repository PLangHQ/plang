namespace app.module.action.@event;

/// <summary>
/// Skips the current action and returns a custom value instead.
/// Use inside a beforeAction event handler to prevent the real action from running: its value, marked Handled,
/// travels back as the handler's answer, and a Handled answer before an action cancels it.
/// </summary>
[Action("skipAction", Cacheable = false)]
public partial class SkipAction : IContext
{
    /// <summary>Value to return instead of the action's real result. Null returns empty success.</summary>
    public partial data.@this Value { get; init; }

    public async Task<data.@this> Start()
    {
        var skipped = Data((Value == null ? null : await Value.Value()));
        skipped.Handled = true;
        return skipped;
    }
}
