using App.Errors;

namespace App.modules.callback;

/// <summary>
/// PLang's <c>- run %callback%</c> verb. Stage 2a.6: collapsed to ~10 lines —
/// Data is verified by construction (the wire deserialiser enforces this
/// before producing a Data at all), so no explicit verify call here. If the
/// Data carries a Snapshot, delegate to <c>Snapshot.Resume(ctx)</c>; otherwise
/// raise a clear error.
/// </summary>
[ModuleDescription("Run a callback — resume execution from the Data's Snapshot.")]
[System.ComponentModel.Description("Resume from a Data's Snapshot")]
[Action("run", Cacheable = false)]
public partial class run : IContext
{
    /// <summary>The callback envelope to run. Must carry a Snapshot.</summary>
    [IsNotNull]
    public partial Data.@this Callback { get; init; }

    public async Task<Data.@this> Run()
    {
        if (Callback.Snapshot == null)
            return global::App.Data.@this.FromError(new ServiceError(
                "Resume invoked on Data without a Snapshot", "NoSnapshot", 400));
        return await Callback.Snapshot.Resume(Context);
    }
}
