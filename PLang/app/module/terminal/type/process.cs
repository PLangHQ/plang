using app.Attributes;

namespace app.module.terminal;

/// <summary>
/// A program started with <c>terminal.open</c> that keeps running: lines it writes arrive through
/// OnOutput/OnError as <c>%!data%</c>, <c>terminal.send</c> writes a line to its stdin,
/// <c>terminal.wait</c> waits for it to exit, <c>terminal.stop</c> ends it.
/// </summary>
[PlangType("process")]
public sealed class Process : global::app.type.item.@this, global::app.type.item.ICreate<Process>
{
    /// <summary>The program's full path.</summary>
    [LlmBuilder, Out] public string Program { get; set; } = "";

    /// <summary>The operating system's process id.</summary>
    [LlmBuilder, Out] public int Id { get; set; }

    /// <summary>True until the program exits.</summary>
    [LlmBuilder, Out] public bool Running => Os is { HasExited: false };

    internal System.Diagnostics.Process? Os { get; set; }
    internal SemaphoreSlim Writing { get; } = new(1, 1);
    internal Task? Reading { get; set; }

    public override string ToString() => $"{Program} (pid {Id}{(Running ? "" : ", exited")})";
}
