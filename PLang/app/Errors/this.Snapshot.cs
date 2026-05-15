namespace app.Errors;

public sealed partial class @this : ISnapshot
{
    /// <summary>
    /// Routes capture into a "trail" subsection so the App snapshot tree mirrors
    /// the live shape (App.Errors.Trail).
    /// </summary>
    public void Capture(Snapshot.@this s) => Trail.Capture(s.Section("trail"));

    /// <summary>
    /// Dispatches to Trail.Restore, which replaces the live Errors.Trail with
    /// a frozen one populated from the snapshot.
    /// </summary>
    public static void Restore(Snapshot.@this s, Actor.Context.@this ctx)
    {
        if (!s.HasSection("trail")) return;
        global::app.Errors.Trail.@this.Restore(s.Section("trail"), ctx);
    }
}
