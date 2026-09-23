namespace app.variable.list;

public partial class @this : ISnapshot
{
    /// <summary>The variables' section.</summary>
    public string Section => "Variables";

    /// <summary>
    /// Captures user-visible variables into the snapshot. Honours the existing
    /// partition (the <see cref="Snapshot()"/> method documents the rules):
    ///  - skip !-prefix (infrastructure vars: !app, !fileSystem, !error, …)
    ///  - skip DynamicData (Now, NowUtc, GUID — always-fresh)
    /// Each remaining variable is cloned so the snapshot is detached from
    /// further mutations on the live store. Full Data shape is preserved
    /// (Name, Value, Type, Properties) — not just key→value. Settings is a
    /// navigable resolver (not a Data subclass), so it never appears in
    /// _variables and needs no special-case here.
    /// </summary>
    public void Capture(snapshot.@this s)
    {
        // Each captured variable is its own entry of the section — the snapshot's native shape, a
        // dict of Data — so %snap.variables.x% navigates straight to it.
        foreach (var kvp in _variables)
        {
            if (kvp.Key.StartsWith("!")) continue;
            // Always-fresh cells (Now, NowUtc, GUID, MyIdentity) — the lazy
            // lives on the computed instance now, so check the instance, not
            // the Data subtype (a store re-wrap loses the subtype).
            if (kvp.Value is data.DynamicData
                || kvp.Value.Item is global::app.type.item.computed) continue;
            s.Entries.Set(kvp.Value.Clone());
        }
    }

    /// <summary>
    /// Restores user variables into this list. System variables created by the constructor
    /// (Now, NowUtc, GUID) are left in place — the snapshot only carries user-visible state, so
    /// adding restored entries on top is the correct merge.
    /// </summary>
    public System.Threading.Tasks.Task Restore(snapshot.@this s, actor.context.@this context)
    {
        // Every entry of the section IS a captured variable (edited in place or not) — cloned so the
        // snapshot can be re-restored independently.
        foreach (var entry in s.Entries.Entries)
            Set(entry.Name, entry.Clone());
        return System.Threading.Tasks.Task.CompletedTask;
    }

}
