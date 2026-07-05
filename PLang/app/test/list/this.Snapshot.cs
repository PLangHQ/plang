namespace app.test.list;

public sealed partial class @this : ISnapshot
{
    /// <summary>
    /// Presence bit: the section exists only when a test session is live (app captures
    /// via Test?.Capture). Run state (the tests, Coverage, Current) and config
    /// (Timeout, Parallel, Include/Exclude, Verbose, Format) reconstruct on build.
    /// </summary>
    public void Capture(snapshot.@this s) { }

    /// <summary>
    /// Section present → a session was live → born it back (presence IS the enable signal).
    /// </summary>
    public static void Restore(snapshot.@this s, actor.context.@this context)
        => context.App.Test = new @this(context);

    public static void Read(snapshot.Io io, snapshot.@this section) { }
}
