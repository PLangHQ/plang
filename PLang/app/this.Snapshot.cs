namespace app;

public sealed partial class @this : global::app.snapshot.ISnapshot
{
    /// <summary>The App's snapshot owners, in restore order — providers first, since a later owner may
    /// consume them. Each names its own section; adding an owner is adding it here.</summary>
    private IEnumerable<global::app.snapshot.ISnapshot> Snapshotted(
        global::app.snapshot.ISnapshot variables, global::app.snapshot.ISnapshot callStack)
        => [Code, variables, Statics, this, callStack];

    /// <summary>
    /// The App's state as a snapshot: each owner captures its own section. Only owners implementing
    /// <see cref="global::app.snapshot.ISnapshot"/> participate — the rest (Modules, Goals, Channels,
    /// Cache, Events, Settings, Types, Config, FileSystem, …) reconstruct on build.
    /// </summary>
    public snapshot.@this Snapshot(actor.context.@this context)
        => Capture(context, Snapshotted(context.Variable, context.CallStack));

    /// <summary>
    /// Throw-time snapshot for an error callback. By the time an error reaches its handler the live
    /// CallStack has unwound past the failing action, so the variables and the call stack are taken
    /// as they stood at the throw (<see cref="global::app.variable.list.@this.SnapshotAt"/>, the chain
    /// the error carried). Everything else is unchanged across handling, so it captures live.
    /// </summary>
    public snapshot.@this Snapshot(global::app.error.Error error, actor.context.@this context)
        => Capture(context, Snapshotted(context.Variable.SnapshotAt(error), context.CallStack.At(error.CallFrames)));

    private snapshot.@this Capture(actor.context.@this context, IEnumerable<global::app.snapshot.ISnapshot> owners)
    {
        var s = new snapshot.@this(context);
        foreach (var owner in owners) owner.Capture(s.Section(owner.Section));
        return s;
    }

    /// <summary>
    /// Restores each captured section into its owner on this App, in <see cref="Snapshotted"/>
    /// order. Hard-errors propagate (e.g. <see cref="Providers.ProviderRestoreException"/>): the App
    /// is left partially restored and the caller treats the failure as a referent-integrity violation.
    /// </summary>
    public async System.Threading.Tasks.Task Restore(snapshot.@this s, actor.context.@this context)
    {
        foreach (var owner in Snapshotted(context.Variable, context.CallStack))
            if (s.HasSection(owner.Section))
                await owner.Restore(s.Section(owner.Section), context);
    }

    // The App as its own owner: its section carries its Mode.
    string global::app.snapshot.ISnapshot.Section => "App";

    void global::app.snapshot.ISnapshot.Capture(snapshot.@this s)
        => s.Write("mode", Mode);

    async System.Threading.Tasks.Task global::app.snapshot.ISnapshot.Restore(snapshot.@this s, actor.context.@this context)
    {
        var entry = s.Entries.Get("mode", context);
        if (entry == null) return;
        var mode = (await entry.Value<global::app.type.item.choice.@this<global::app.Mode>>())!.Value;
        Build = mode == global::app.Mode.Build ? new global::app.module.action.build.@this(context) : null;
        Test = mode == global::app.Mode.Test ? new global::app.test.list.@this(context) : null;
    }
}
