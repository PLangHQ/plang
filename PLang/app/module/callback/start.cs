using app.error;

namespace app.module.callback;

/// <summary>
/// PLang's <c>- start %callback%</c> verb. Data is verified by construction (the wire deserialiser
/// enforces this before producing a Data at all), so no explicit verify call here. If the Data
/// carries a Snapshot, delegate to <c>Snapshot.Resume(context)</c>; otherwise raise a clear error.
/// </summary>
[Action("start", Cacheable = false)]
public partial class start : IContext
{
    /// <summary>The callback Data to start. Must carry a Snapshot.</summary>
    [IsNotNull]
    public partial data.@this Callback { get; init; }

    public async Task<data.@this> Start()
    {
        if (Callback.Snapshot == null)
            return Context.Error(new ServiceError(
                "Resume invoked on Data without a Snapshot", "NoSnapshot", 400));
        return (await Callback.Snapshot.Resume(Context));
    }
}
