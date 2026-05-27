using app.modules;

namespace app.channels.channel.message;

/// <summary>
/// Channel pattern abstract: stateless, one-shot exchange.
/// AskCore returns <see cref="modules.output.Ask"/>-typed Data with a Snapshot
/// attached — the engine short-circuits via <c>Data.ShouldExit()</c>, the
/// channel layer serialises the Snapshot to the wire, and resume re-enters
/// via <c>Data.Snapshot.Resume(ctx)</c>. Web extends Message (when shipped).
/// </summary>
public abstract class @this : Channel
{
    public override Task<data.@this> AskCore(modules.output.ask action, CancellationToken ct = default)
    {
        // Suspend Ask: Value is an Ask instance with no Answer bound — IExitsGoal.
        // ShouldExit() returns true (Answer==null), so the step loop short-circuits
        // and the Snapshot carries enough state for the channel to resume the goal
        // once the user replies. Type="ask" also still satisfies the Type-side
        // Exit check, which keeps the legacy Type-only flow paths working.
        var ask = new modules.output.Ask();
        var d = new data.@this<modules.output.Ask>("", ask, new data.type("ask"))
        {
            Context = action.Context,
            Snapshot = action.Snapshot(),
        };
        return Task.FromResult<data.@this>(d);
    }
}
