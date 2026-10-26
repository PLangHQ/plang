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

    private data.@this<snapshot.@this> Snapshot(Dictionary<snapshot.ISnapshot, snapshot.ISnapshot> thrown, actor.context.@this context)
    {
        if (!ReferenceEquals(context, context.Actor.Context))
            return global::app.data.@this<snapshot.@this>.FromError(new global::app.error.@this(
                "a snapshot is saved from the run's own context, not from inside a task: a task's variables and frames are reached by nothing a snapshot saves",
                "SnapshotInTask", 400), context);
        var snapshot = new snapshot.@this(context);
        var stored = snapshot.Store(this, thrown, context);
        return stored.Success ? context.Ok(snapshot) : global::app.data.@this<snapshot.@this>.FromError(stored.Error!, context);
    }

    // The app as the root part: the members it keeps. Its mode is not stored: how an app was started (test, build, run)
    // is the host's choice, not the program's state, so a restore leaves the app as it was started.
    data.@this global::app.snapshot.ISnapshot.Store(snapshot.@this snapshot, actor.context.@this context)
    {
        snapshot.Write("kept", Kept.Dict(context.App.type.list));
        return context.Ok();
    }

    async System.Threading.Tasks.Task<data.@this> global::app.snapshot.ISnapshot.Start(snapshot.@this snapshot, actor.context.@this context)
    {
        // taken in this app's context, as every restored Data is; a value that doesn't read is its entry's answer
        if (snapshot.In("kept", context) is { } kept)
        {
            if (await kept.Value<global::app.type.item.dict.@this>() is not { } members) return kept;
            Kept.Replace(members, context);
        }
        return context.Ok();
    }
}
