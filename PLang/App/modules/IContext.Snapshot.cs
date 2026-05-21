namespace App.modules;

/// <summary>
/// Convenience for action handlers: <c>this.Snapshot()</c> captures App
/// state from the action's perspective while the Call frame is still alive.
/// Called by handlers that return Exit-typed Data (e.g. Message channel's
/// <c>Ask</c>).
/// </summary>
public static class IContextSnapshotExtensions
{
    public static global::App.Snapshot.@this Snapshot(this IContext handler)
        => handler.Context.App.Snapshot();
}
