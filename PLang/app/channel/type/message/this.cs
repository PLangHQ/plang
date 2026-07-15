using app.module;

namespace app.channel.type.message;

/// <summary>
/// Channel pattern abstract: stateless, one-shot exchange.
/// Ask returns <see cref="module.action.output.Ask"/>-typed Data with a Snapshot
/// attached — the engine short-circuits via <c>Data.ShouldExit()</c>, the
/// channel layer serialises the Snapshot to the wire, and resume re-enters
/// via <c>Data.Snapshot.Resume(context)</c>. Web extends Message (when shipped).
/// </summary>
public abstract class @this : Channel
{
    public override Task<data.@this> Ask(module.action.output.ask action, CancellationToken ct = default)
    {
        // Suspend Ask: Value is an Ask with no Answer bound, so IExitsGoal.ShouldExit()
        // returns true and the step loop short-circuits. Snapshot carries enough
        // state for the channel to resume the goal once the user replies. Type="ask"
        // also satisfies the Type-side Exit check.
        var ask = new module.action.output.Ask();
        var d = new data.@this<module.action.output.Ask>("", ask, new app.type.@this("ask"), context: action.Context)
        {
            Snapshot = action.Snapshot(),
        };
        return Task.FromResult<data.@this>(d);
    }
}
