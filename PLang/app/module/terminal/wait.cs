namespace app.module.terminal;

/// <summary>Waits until a running program (from <c>terminal.open</c>) exits; returns its exit code. Its
/// remaining output lines are delivered before this returns.</summary>
[Action("wait", Cacheable = false)]
public partial class wait : IContext
{
    /// <summary>The running program, from <c>terminal.open</c>.</summary>
    public partial data.@this<type.process.@this> Process { get; init; }

    public async Task<data.@this<global::app.type.item.number.@this>> Start()
        => await Process.Value() is { } running ? await running.Wait(Context)
            : data.@this<global::app.type.item.number.@this>.From(Context.Error(new global::app.error.ActionError("No running program to wait for.", "ProgramNotRunning", 409)));
}
