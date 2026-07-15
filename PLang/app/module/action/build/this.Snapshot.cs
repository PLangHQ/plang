namespace app.module.action.build;

public sealed partial class @this : ISnapshot
{
    /// <summary>
    /// Presence bit: the "Build" section exists only when Build is on (app captures via
    /// Build?.Capture, so the section is created only in that path). Nothing to write —
    /// the section's existence IS the signal; the other fields reconstruct on build.
    /// </summary>
    public void Capture(global::app.snapshot.@this s) { }

    /// <summary>
    /// Section present → Build was on → born it back (presence IS the enable signal).
    /// </summary>
    public static void Restore(global::app.snapshot.@this s, global::app.actor.context.@this context)
        => context.App.Build = new global::app.module.action.build.@this(context);
}
