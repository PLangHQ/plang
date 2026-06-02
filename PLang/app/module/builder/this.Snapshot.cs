namespace app.module.builder;

public sealed partial class @this : ISnapshot
{
    /// <summary>
    /// Captures the build-mode bit. The other fields (Files filter, Cache flag,
    /// _prSnapshot dictionary) are reconstruct-on-build — they're config inputs
    /// or scratch state, not in-scope state worth carrying across resume.
    /// </summary>
    public void Capture(global::app.snapshot.@this s) => s.Write("isEnabled", IsEnabled);

    /// <summary>
    /// Restores the build-mode bit on the live App.Builder instance.
    /// </summary>
    public static void Restore(global::app.snapshot.@this s, global::app.actor.context.@this context)
        => context.App.Builder.IsEnabled = s.Read<bool>("isEnabled");

    public static void Read(global::app.snapshot.Io io, global::app.snapshot.@this section)
        => section.Write("isEnabled", io.Get<bool>("isEnabled"));
}
