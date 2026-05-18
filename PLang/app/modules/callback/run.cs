using app.errors;

namespace app.modules.callback;

/// <summary>
/// PLang's <c>- run %callback%</c> verb. Always seals the Data (lazy-signs if not already
/// signed via <see cref="data.@this.EnsureSigned"/>), verifies via <c>signing.verify</c>,
/// then dispatches into the typed Callback's own <c>Run(ctx)</c>. In-process and wire paths
/// look identical to the gate: absence-of-signature is rejected, never trusted (S-F1).
/// </summary>
[ModuleDescription("Run a callback — seal, verify, then dispatch into the typed callback's Run.")]
[System.ComponentModel.Description("Verify and dispatch a callback")]
[Action("run", Cacheable = false)]
public partial class run : IContext
{
    /// <summary>The callback envelope to run. Must wrap an ICallback value.</summary>
    [IsNotNull]
    public partial data.@this Callback { get; init; }

    public async Task<data.@this> Run()
    {
        if (Callback.Value is not global::app.modules.callback.ICallback cb)
            return global::app.data.@this.FromError(new ServiceError(
                "- run %x% requires an ICallback value", "TypeError", 400));

        // Seal first. In-process callback values get signed locally (data.Context provides
        // the identity); wire-deserialized values that already carry a signature short-circuit
        // (EnsureSigned no-ops); wire values with no Context throw — that path is rejected
        // below with MissingCallbackSignature, never trusted (auditor v2 / security v1 S-F1).
        try
        {
            Callback.EnsureSigned();
        }
        catch (System.InvalidOperationException ex)
        {
            return global::app.data.@this.FromError(new ServiceError(
                $"Callback cannot be sealed for verification: {ex.Message}",
                "MissingCallbackSignature", 400));
        }

        if (Callback.RawSignature == null)
            return global::app.data.@this.FromError(new ServiceError(
                "Callback has no signature after EnsureSigned — cannot verify",
                "MissingCallbackSignature", 400));

        var verifyResult = await Context.App.RunAction<global::app.modules.signing.verify>(
            new global::app.modules.signing.verify { Data = Callback }, Context);
        if (!verifyResult.Success)
            return global::app.data.@this.FromError(new ServiceError(
                $"Callback signature verification failed: {verifyResult.Error?.Message ?? "unknown"}",
                "CallbackSignatureMismatch", 400));

        // Wrap CLR exceptions out of the typed callback's Run (e.g. CallbackGoalNotFound /
        // CallbackGoalHashMismatch from PositionWire.Resolve, or InvalidOperationException
        // out of Restore) so the public entry never leaks raw exceptions to the channel.
        // Auditor v1 N3 / security v1 adjacent-risks.
        try
        {
            return await cb.Run(Context);
        }
        catch (CallbackGoalNotFound ex)
        {
            return global::app.data.@this.FromError(new ServiceError(
                ex.Message, "CallbackGoalNotFound", 404));
        }
        catch (CallbackGoalHashMismatch ex)
        {
            return global::app.data.@this.FromError(new ServiceError(
                ex.Message, "CallbackGoalHashMismatch", 409));
        }
        catch (System.InvalidOperationException ex)
        {
            return global::app.data.@this.FromError(new ServiceError(
                $"Callback dispatch failed: {ex.Message}", "CallbackDispatchError", 500));
        }
    }
}
