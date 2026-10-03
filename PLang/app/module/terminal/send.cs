namespace app.module.terminal;

/// <summary>Writes a line to a running program's stdin (one started with <c>terminal.open</c>).</summary>
[Action("send", Cacheable = false)]
public partial class send : IContext
{
    /// <summary>What to write. Text goes as-is; anything else as its text form. A newline ends the line.</summary>
    public partial data.@this Data { get; init; }

    /// <summary>The running program, from <c>terminal.open</c>.</summary>
    public partial data.@this<type.process.@this> Process { get; init; }

    public async Task<data.@this> Start()
        => await Process.Value() is { } running ? await running.Send(Data, Context)
            : Context.Error(new global::app.error.ActionError("No running program to send to.", "ProgramNotRunning", 409));
}
