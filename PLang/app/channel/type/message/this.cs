using app.module;

namespace app.channel.type.message;

/// <summary>
/// Channel pattern abstract: stateless, one-shot exchange.
/// Ask returns <see cref="module.output.Ask"/>-typed Data with a Snapshot
/// attached — the engine short-circuits via <c>Data.ShouldExit()</c>, the
/// channel layer serialises the Snapshot to the wire, and resume re-enters
/// via <c>Data.Snapshot.Resume(context)</c>. Web extends Message (when shipped).
/// </summary>
public abstract class @this : Channel
{
    public override Task<data.@this> Ask(module.output.ask action, CancellationToken ct = default)
    {
        // A pending Ask: an IExitsGoal, so the step loop short-circuits. Snapshot carries enough
        // state for the channel to resume the goal once the user replies.
        var pending = action.Context.Ok<module.output.Ask>(new module.output.Ask { Waiting = true });
        pending.Snapshot = action.Snapshot();
        return Task.FromResult<data.@this>(pending);
    }
}
