using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.engine;

/// <summary>
/// Kernel step dispatch — runs a step's actions directly.
/// No retry, no events, no error handling. The PLang runtime wraps this.
/// </summary>
[Action("execute")]
public partial class Execute : IContext
{
    [IsNotNull]
    public partial Step Step { get; init; }

    public async Task<Data> Run()
    {
        return await Step.Actions.RunAsync(Context.Engine!, Context, Context.CancellationToken);
    }
}
