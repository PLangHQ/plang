namespace App.Builder;

public sealed partial class @this : ISnapshotted
{
    /// <summary>
    /// Captures the build-mode bit. The other fields (Files filter, Cache flag,
    /// _prSnapshot dictionary) are reconstruct-on-build — they're config inputs
    /// or scratch state, not in-scope state worth carrying across resume.
    /// </summary>
    public void Capture(Snapshot.@this s) => s.Write("isEnabled", IsEnabled);

    /// <summary>
    /// Restores the build-mode bit on the live App.Builder instance.
    /// </summary>
    public static void Restore(Snapshot.@this s, Actor.Context.@this ctx)
        => ctx.App.Builder.IsEnabled = s.Read<bool>("isEnabled");
}
