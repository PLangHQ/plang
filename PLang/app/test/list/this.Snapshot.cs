namespace app.test.list;

public sealed partial class @this : ISnapshot
{
    /// <summary>
    /// Captures the test-mode bit. Run state (the tests, Coverage, Current) and
    /// configuration (Timeout, Parallel, Include/Exclude, Verbose, Format) are
    /// reconstruct-on-build — they're per-run state or CLI input, not in-scope
    /// state worth carrying across resume.
    /// </summary>
    public void Capture(snapshot.@this s) => s.Write("isEnabled", IsEnabled);

    /// <summary>
    /// Restores the test-mode bit on the live App.Test instance.
    /// </summary>
    public static void Restore(snapshot.@this s, actor.context.@this context)
        => context.App.Test.IsEnabled = s.Read<bool>("isEnabled");

    public static void Read(snapshot.Io io, snapshot.@this section)
        => section.Write("isEnabled", io.Get<bool>("isEnabled"));
}
