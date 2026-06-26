using app.error;

namespace app.module.snapshot;

/// <summary>
/// PLang's <c>- resume %snap%</c> verb. The parameter is declared
/// <c>Data&lt;snapshot.@this&gt;</c>, so the runtime converts whatever
/// <c>%snap%</c> holds — typically the wire bytes just read off disk — into the
/// snapshot object at the action boundary (the lazy-param <c>As&lt;T&gt;</c>
/// path → <c>snapshot.@this.FromWire</c>). Read stays dumb; the type-driven
/// rebuild is pulled here by declaring the type.
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

    public async Task<data.@this> Run()
    {
        var snap = await Snapshot.Value();
        if (snap == null)
            return Context.Error(new ServiceError(
                "resume could not rebuild a snapshot from the value", "NoSnapshot", 400));
        return await snap.Resume(Context);
    }
}
