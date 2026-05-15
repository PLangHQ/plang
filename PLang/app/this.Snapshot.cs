namespace app;

public sealed partial class @this
{
    /// <summary>
    /// Walks the App's ISnapshot properties and aggregates them into a typed
    /// Snapshot tree. Only subsystems implementing <see cref="ISnapshot"/>
    /// participate — the ones that don't (Modules, Goals, Channels, Cache, Events,
    /// Settings, Navigators, Types, Config, FileSystem, …) are reconstruct-on-build
    /// and stay invisible to the snapshot.
    ///
    /// The wire shape mirrors the App tree: each subsystem owns a named section.
    /// Adding a new subsystem to the snapshot is just implementing ISnapshot
    /// and adding a one-liner here — no central registry, no ordering coupling.
    /// </summary>
    public snapshot.@this Snapshot()
    {
        var s = new snapshot.@this();
        CurrentActor.Context.Variables.Capture(s.Section("Variables"));
        Errors.Capture(s.Section("Errors"));
        Code.Capture(s.Section("Providers"));
        Statics.Capture(s.Section("Statics"));
        Builder.Capture(s.Section("Build"));
        Tester.Capture(s.Section("Testing"));
        CallStack.Capture(s.Section("CallStack"));
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
    public void Restore(snapshot.@this s, actor.context.@this? context = null)
    {
        var ctx = context ?? CurrentActor.Context;

        if (s.HasSection("Providers")) global::app.Code.@this.Restore(s.Section("Providers"), ctx);
        if (s.HasSection("Variables")) global::app.variables.@this.Restore(s.Section("Variables"), ctx);
        if (s.HasSection("Errors"))    global::app.errors.@this.Restore(s.Section("Errors"), ctx);
        if (s.HasSection("Statics"))   global::app.Statics.@this.Restore(s.Section("Statics"), ctx);
        if (s.HasSection("Build"))     global::app.Builder.@this.Restore(s.Section("Build"), ctx);
        if (s.HasSection("Testing"))   global::app.tester.@this.Restore(s.Section("Testing"), ctx);
        if (s.HasSection("CallStack")) global::app.callstack.@this.Restore(s.Section("CallStack"), ctx);
    }
}
