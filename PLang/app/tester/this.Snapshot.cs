namespace app.tester;

public sealed partial class @this : ISnapshot
{
    /// <summary>
    /// Captures the test-mode bit. Run state (Results, Coverage, CurrentTest) and
    /// configuration (Timeout, Parallel, Include/Exclude, Verbose, Format) are
    /// reconstruct-on-build — they're per-run state or CLI input, not in-scope
    /// state worth carrying across resume.
    /// </summary>
    public void Capture(snapshot.@this s) => s.Write("isEnabled", IsEnabled);

    /// <summary>
    /// Restores the test-mode bit on the live App.Tester instance.
    /// </summary>
    public static void Restore(snapshot.@this s, actor.context.@this context)
        => context.App.Tester.IsEnabled = s.Read<bool>("isEnabled");
}
