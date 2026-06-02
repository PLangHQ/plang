namespace app.error.list;

public sealed partial class @this : ISnapshot
{
    /// <summary>
    /// Routes capture into a "trail" subsection so the App snapshot tree mirrors
    /// the live shape (App.Errors.Trail).
    /// </summary>
    public void Capture(global::app.snapshot.@this s) => Trail.Capture(s.Section("trail"));

    /// <summary>
    /// Dispatches to Trail.Restore, which replaces the live Errors.Trail with
    /// a frozen one populated from the snapshot.
    /// </summary>
    public static void Restore(global::app.snapshot.@this s, global::app.actor.context.@this context)
    {
        if (!s.HasSection("trail")) return;
        global::app.error.trail.@this.Restore(s.Section("trail"), context);
    }

    public static void Read(global::app.snapshot.Io io, global::app.snapshot.@this s)
    {
        var sub = io.GetSection("trail");
        if (sub == null) return;
        global::app.error.trail.@this.Read(sub, s.Section("trail"));
    }
}
