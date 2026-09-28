namespace app.module.action.on;

/// <summary>
/// Cancels the running event from inside a call bound before it: what it was about to do does not happen (the
/// action does not start, the value is not written), and <see cref="Value"/> is its result instead.
/// </summary>
[Action("cancel", Cacheable = false)]
public partial class OnCancel : IContext
{
    /// <summary>The result in place of what the event would have made. Omitted: an empty success.</summary>
    public partial data.@this Value { get; init; }

    public async Task<data.@this> Start()
    {
        // a Handled answer before an event cancels it, and is its result
        var cancelled = Data(Value == null ? null : await Value.Value());
        cancelled.Handled = true;
        return cancelled;
    }
}
