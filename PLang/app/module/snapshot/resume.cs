namespace app.module.snapshot;

/// <summary>
/// PLang's <c>- resume %snap%</c> verb. The parameter is declared
/// <c>Data&lt;snapshot.@this&gt;</c>, so the runtime converts whatever
/// <c>%snap%</c> holds — typically the wire bytes just read off disk — into the
/// snapshot object at the action boundary (the lazy-param <c>As&lt;T&gt;</c>
/// path → <c>snapshot.@this.Create</c>, which reads the wire through the plang
/// serializer). Read stays dumb; the type-driven rebuild is pulled here by
/// declaring the type.
///
/// <para>Resuming re-enters the captured CallStack position
/// (<see cref="global::app.snapshot.@this.Resume"/>) — deterministic, no live LLM.</para>
/// </summary>
[Action("resume", Cacheable = false)]
public partial class resume : IContext
{
    /// <summary>The snapshot to replay. Converted from the read bytes on access.</summary>
    [IsNotNull]
    public partial data.@this<global::app.snapshot.@this> Snapshot { get; init; }

    /// <summary>Resumes the snapshot. A value that isn't one is the answer: the typed ask's own reason when it gave one,
    /// else what the slot holds and the step that reads a snapshot (a file read as a .json is a dict, not a snapshot).</summary>
    public async Task<data.@this> Start()
    {
        if (await Snapshot.Value() is { } snap) return await snap.Resume(Context);
        if (Snapshot.Error is { } error) return Context.Error(error);
        return Context.Error(new global::app.error.@this(
            $"%{Snapshot.Name}% holds a {Snapshot.Peek().Type.Name}, not a snapshot: read it as one (`read file 'run.json', write to %{Snapshot.Name}% as snapshot`)",
            "NoSnapshot", 400));
    }
}
