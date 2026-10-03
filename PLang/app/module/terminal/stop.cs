namespace app.module.terminal;

/// <summary>Ends a running program (from <c>terminal.open</c>) and everything it started.</summary>
[Action("stop", Cacheable = false)]
public partial class stop : IContext
{
    /// <summary>The running program, from <c>terminal.open</c>.</summary>
    public partial data.@this<type.process.@this> Process { get; init; }

    public async Task<data.@this> Start()
        => await Process.Value() is { } running ? running.Stop(Context) : Context.Ok();
}
