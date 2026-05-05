using App.Errors;

namespace App.modules.callback;

/// <summary>
/// PLang's <c>- run %callback%</c> verb. Verifies the wire signature (when present) via
/// <c>signing.verify</c>, then dispatches into the typed Callback's own <c>Run(ctx)</c>.
/// The handler is the gate; <see cref="App.Callback.ICallback.Run"/> assumes verified input.
/// </summary>
[ModuleDescription("Run a callback — verify signature, dispatch into the typed callback's Run.")]
[System.ComponentModel.Description("Verify and dispatch a callback")]
[Action("run", Cacheable = false)]
public partial class run : IContext
{
    /// <summary>The callback envelope to run. Must wrap an ICallback value.</summary>
    [IsNotNull]
    public partial Data.@this Callback { get; init; }

    public async Task<Data.@this> Run()
    {
        if (Callback.Value is not global::App.Callback.ICallback cb)
            return global::App.Data.@this.FromError(new ServiceError(
                "- run %x% requires an ICallback value", "TypeError", 400));

        // Verify signature if present (callbacks coming off the wire have one); skip when
        // the callback was constructed in-process and isn't sealed.
        if (Callback.RawSignature != null)
        {
            var verifyResult = await Context.App.RunAction<global::App.modules.signing.verify>(
                new global::App.modules.signing.verify { Data = Callback }, Context);
            if (!verifyResult.Success)
                return global::App.Data.@this.FromError(new ServiceError(
                    $"Callback signature verification failed: {verifyResult.Error?.Message ?? "unknown"}",
                    "CallbackSignatureMismatch", 400));
        }

        return await cb.Run(Context);
    }
}
