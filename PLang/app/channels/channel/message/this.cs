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
        // Type is "ask" (resolves to typeof(Ask) via PlangType registration) so
        // Type.ClrType.Exit() == true. Value is the question text — single-layer
        // wire shape per the stage 2a design.
        var d = new data.@this("", action.Question?.Value ?? string.Empty, new data.type("ask"))
        {
            Context = action.Context,
            Snapshot = action.Snapshot(),
        };
        return Task.FromResult<data.@this>(d);
    }
}
