namespace app.module;

/// <summary>
/// Convenience for action handlers: <c>this.Snapshot()</c> saves the run from the action's perspective while the Call
/// frame is still alive. Called by handlers that return Exit-typed Data (e.g. Message channel's <c>Ask</c>); a save
/// that can't be made (inside a task) is answered.
/// </summary>
public static class IContextSnapshotExtensions
{
    public static data.@this<global::app.snapshot.@this> Snapshot(this IContext handler)
        => handler.Context.App.Snapshot(handler.Context);
}
