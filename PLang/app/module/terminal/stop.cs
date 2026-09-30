using app.module.terminal.code;

namespace app.module.terminal;

/// <summary>Ends a running program (from <c>terminal.open</c>) and everything it started.</summary>
[Action("stop", Cacheable = false)]
public partial class stop : IContext
{
    /// <summary>The running program, from <c>terminal.open</c>.</summary>
    public partial data.@this<Process> Process { get; init; }

    [Code]
    public partial ITerminal Terminal { get; }

    public async Task<data.@this> Start() => await Terminal.Stop(this);
}
