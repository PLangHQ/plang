using app.module.terminal.code;

namespace app.module.terminal;

/// <summary>Waits until a running program (from <c>terminal.open</c>) exits; returns its exit code. Its
/// remaining output lines are delivered before this returns.</summary>
[Action("wait", Cacheable = false)]
public partial class wait : IContext
{
    /// <summary>The running program, from <c>terminal.open</c>.</summary>
    public partial data.@this<Process> Process { get; init; }

    [Code]
    public partial ITerminal Terminal { get; }

    public async Task<data.@this<global::app.type.item.number.@this>> Start() => await Terminal.Wait(this);
}
