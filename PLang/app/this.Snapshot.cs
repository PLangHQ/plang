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
        var s = new snapshot.@this(CurrentActor.Context);
        CurrentActor.Context.Variable.Capture(s.Section("Variables"));
        Error.Capture(s.Section("Errors"));
        Code.Capture(s.Section("Providers"));
        Statics.Capture(s.Section("Statics"));
        Builder.Capture(s.Section("Build"));
        Tester.Capture(s.Section("Testing"));
        CallStack.Capture(s.Section("CallStack"));
        return s;
    }

    /// <summary>
    /// Throw-time snapshot for an error callback. By the time an error reaches its
    /// handler the live CallStack has unwound past the failing action, so the
    /// snapshot must use the chain the error carried from its throw point
    /// (<see cref="global::app.error.IError.CallFrames"/>) and the throw-time
    /// variable view (<see cref="global::app.variable.list.@this.SnapshotAt"/>).
    /// Everything else (modes, providers, statics) is unchanged across handling,
    /// so it captures live.
    /// </summary>
    public snapshot.@this Snapshot(global::app.error.IError error)
    {
        var s = new snapshot.@this(CurrentActor.Context);
        CurrentActor.Context.Variable.SnapshotAt(error).Capture(s.Section("Variables"));
        Error.Capture(s.Section("Errors"));
        Code.Capture(s.Section("Providers"));
        Statics.Capture(s.Section("Statics"));
        Builder.Capture(s.Section("Build"));
        Tester.Capture(s.Section("Testing"));
        CallStack.Capture(s.Section("CallStack"), error.CallFrames);
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
        context = context ?? CurrentActor.Context;

        if (s.HasSection("Providers")) global::app.module.code.@this.Restore(s.Section("Providers"), context);
        if (s.HasSection("Variables")) global::app.variable.list.@this.Restore(s.Section("Variables"), context);
        if (s.HasSection("Errors"))    global::app.error.list.@this.Restore(s.Section("Errors"), context);
        if (s.HasSection("Statics"))   global::app.Statics.@this.Restore(s.Section("Statics"), context);
        if (s.HasSection("Build"))     global::app.module.builder.@this.Restore(s.Section("Build"), context);
        if (s.HasSection("Testing"))   global::app.tester.@this.Restore(s.Section("Testing"), context);
        if (s.HasSection("CallStack")) global::app.callstack.@this.Restore(s.Section("CallStack"), context);
    }
}
