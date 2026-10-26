namespace app;

public sealed partial class @this : global::app.snapshot.ISnapshot
{
    /// <summary>
    /// The run's state as a snapshot: the app and every part under it (<see cref="snapshot.@this.Store(snapshot.ISnapshot, actor.context.@this)"/>),
    /// each at its place. A save inside a task is refused: a task's context is no member of anything the walk reaches, so
    /// its variables and its frames would be missing from what was saved.
    /// </summary>
    public data.@this<snapshot.@this> Snapshot(actor.context.@this context)
        => Snapshot(new Dictionary<snapshot.ISnapshot, snapshot.ISnapshot>(ReferenceEqualityComparer.Instance), context);

    /// <summary>
    /// Throw-time snapshot for an error callback. By the time an error reaches its handler the live call stack has
    /// unwound past the failing action, so the asker's variables and call stack are written as they stood at the throw
    /// (<see cref="global::app.type.item.variable.list.@this.SnapshotAt"/>, the chain the error carried). Everything else
    /// is unchanged across handling, so it is stored live.
    /// </summary>
    public data.@this<snapshot.@this> Snapshot(global::app.error.@this error, actor.context.@this context)
        => Snapshot(new Dictionary<snapshot.ISnapshot, snapshot.ISnapshot>(ReferenceEqualityComparer.Instance)
        {
            [context.Variable] = context.Variable.SnapshotAt(error),
            [context.call] = context.call.At(error.CallFrames),
        }, context);

    private data.@this<snapshot.@this> Snapshot(Dictionary<snapshot.ISnapshot, snapshot.ISnapshot> standIns, actor.context.@this context)
    {
        if (!ReferenceEquals(context, context.Actor.Context))
            return global::app.data.@this<snapshot.@this>.FromError(new global::app.error.@this(
                "a snapshot is saved from the run's own context, not from inside a task: a task's variables and frames are reached by nothing a snapshot saves",
                "SnapshotInTask", 400), context);
        var snapshot = new snapshot.@this(context);
        var stored = snapshot.Store(this, standIns, context);
        return stored.Success ? context.Ok(snapshot) : global::app.data.@this<snapshot.@this>.FromError(stored.Error!, context);
    }

    // The app as the root part: its mode, and the members it keeps.
    data.@this global::app.snapshot.ISnapshot.Store(snapshot.@this snapshot, actor.context.@this context)
    {
        var types = context.App.type.list;
        snapshot.Entries.Set(new data.@this("mode",
            new global::app.type.item.choice.@this<global::app.Mode>(Mode, types[typeof(global::app.type.item.choice.@this<global::app.Mode>)]),
            context: context));
        var kept = new global::app.type.item.dict.@this(types[typeof(global::app.type.item.dict.@this)]);
        foreach (var (name, member) in Kept.Members) kept.Set(name, member);
        snapshot.Entries.Set(new data.@this("kept", kept, context: context));
        return context.Ok();
    }

    async System.Threading.Tasks.Task<data.@this> global::app.snapshot.ISnapshot.Start(snapshot.@this snapshot, actor.context.@this context)
    {
        if (snapshot.Entries.Get("mode", context) is { } entry)
        {
            var mode = (await entry.Value<global::app.type.item.choice.@this<global::app.Mode>>())!.Value;
            Build = mode == global::app.Mode.Build ? new global::app.module.build.@this(context) : null;
            if (mode == global::app.Mode.Test && test.list.Session == null) await test.list.Open();
            else if (mode != global::app.Mode.Test) await test.list.Close();
        }
        if (snapshot.Entries.Get("kept", context) is { } kept)
            Kept.Replace((await kept.Value<global::app.type.item.dict.@this>())!, context);
        return context.Ok();
    }
}
