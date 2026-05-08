namespace App.Variables;

public partial class @this : ISnapshotted
{
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
    public void Capture(Snapshot.@this s)
    {
        var captured = new List<Data.@this>();
        foreach (var kvp in _variables)
        {
            if (kvp.Key.StartsWith("!")) continue;
            if (kvp.Value is Data.DynamicData) continue;
            captured.Add(kvp.Value.Clone());
        }
        s.Write("variables", captured);
    }

    /// <summary>
    /// Restores user variables onto the live App's CurrentActor.Variables. System
    /// variables created by the constructor (Now, NowUtc, GUID) are left in place
    /// — the snapshot only carries user-visible state, so adding restored entries
    /// on top is the correct merge.
    /// </summary>
    public static void Restore(Snapshot.@this s, Actor.Context.@this ctx)
    {
        var captured = s.Read<List<Data.@this>>("variables");
        if (captured == null) return;

        var target = ctx.Variables;
        foreach (var data in captured)
        {
            // Clone again so the snapshot can be re-Restored independently.
            target.Set(data.Name, data.Clone());
        }
    }
}
