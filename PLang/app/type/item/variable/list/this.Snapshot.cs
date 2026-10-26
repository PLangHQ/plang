namespace app.type.item.variable.list;

public partial class @this : ISnapshot
{
    /// <summary>
    /// Writes the user-visible variables, each its own entry, the Data whole (name, value, type, properties), cloned so
    /// the snapshot is detached from later changes. Left out:
    ///  - !-prefixed infrastructure variables (!app, !filesystem, !error, …);
    ///  - always-fresh cells (Now, NowUtc, GUID, MyIdentity), which are made again where they are read;
    ///  - a sensitive value: a resumed run that needs it asks again.
    /// </summary>
    data.@this ISnapshot.Store(global::app.snapshot.@this snapshot, actor.context.@this context)
    {
        foreach (var kvp in _variables)
        {
            if (IsSystem(kvp.Key)) continue;
            // the lazy lives on the computed instance, so check the instance, not the Data subtype (a store re-wrap
            // loses the subtype)
            if (kvp.Value is data.DynamicData
                || kvp.Value.Item is global::app.type.item.computed) continue;
            if (kvp.Value.Item is global::app.type.item.sensitive.@this) continue;
            // under the store's key, the truth: one Data held under two names is stored under both
            snapshot.Entries.Set(kvp.Key, kvp.Value.Clone());
        }
        return context.Ok();
    }

    /// <summary>
    /// Puts the stored variables back into this list, on top of the system variables it was born with (Now, NowUtc,
    /// GUID): the snapshot only holds user-visible state. Each comes in this list's context (the snapshot's <c>In</c>),
    /// so it answers as this actor, whoever started the restore. A set can wait (its before and after events, a value
    /// that binds others), so this answers when the last one has finished.
    /// </summary>
    async System.Threading.Tasks.Task<data.@this> ISnapshot.Start(global::app.snapshot.@this snapshot, actor.context.@this context)
    {
        foreach (var entry in snapshot.In(context))
            await Set(entry.Name, entry);
        return context.Ok();
    }
}
