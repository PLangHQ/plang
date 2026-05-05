namespace App;

public sealed partial class @this
{
    /// <summary>
    /// Walks the App's ISnapshotted properties and aggregates them into a typed
    /// Snapshot tree. Only subsystems implementing <see cref="ISnapshotted"/>
    /// participate — the ones that don't (Modules, Goals, Channels, Cache, Events,
    /// Settings, Navigators, Types, Config, FileSystem, …) are reconstruct-on-build
    /// and stay invisible to the snapshot.
    ///
    /// The wire shape mirrors the App tree: each subsystem owns a named section.
    /// Adding a new subsystem to the snapshot is just implementing ISnapshotted
    /// and adding a one-liner here — no central registry, no ordering coupling.
    /// </summary>
    public Snapshot.@this Snapshot()
    {
        var s = new Snapshot.@this();
        Variables.Capture(s.Section("Variables"));
        Errors.Capture(s.Section("Errors"));
        Providers.Capture(s.Section("Providers"));
        Statics.Capture(s.Section("Statics"));
        Build.Capture(s.Section("Build"));
        Testing.Capture(s.Section("Testing"));
        Debug.CallStack.Capture(s.Section("CallStack"));
        return s;
    }

    /// <summary>
    /// Dispatches each captured subtree to the matching subsystem's static
    /// Restore. Order matches Snapshot — Providers' two-step replay must run
    /// before any subsystem that might consume providers, but for the Stage 1
    /// inventory the subsystems are independent.
    ///
    /// Hard-errors propagate (e.g. <see cref="Providers.ProviderRestoreException"/>):
    /// the App is left in a partially-restored state and the caller is responsible
    /// for treating the failure as a referent-integrity violation.
    /// </summary>
    public void Restore(Snapshot.@this s, Actor.Context.@this? context = null)
    {
        var ctx = context ?? Context;

        if (s.HasSection("Providers")) global::App.Providers.@this.Restore(s.Section("Providers"), ctx);
        if (s.HasSection("Variables")) global::App.Variables.@this.Restore(s.Section("Variables"), ctx);
        if (s.HasSection("Errors"))    global::App.Errors.@this.Restore(s.Section("Errors"), ctx);
        if (s.HasSection("Statics"))   global::App.Statics.@this.Restore(s.Section("Statics"), ctx);
        if (s.HasSection("Build"))     global::App.Build.@this.Restore(s.Section("Build"), ctx);
        if (s.HasSection("Testing"))   global::App.Test.@this.Restore(s.Section("Testing"), ctx);
        if (s.HasSection("CallStack")) global::App.CallStack.@this.Restore(s.Section("CallStack"), ctx);
    }
}
