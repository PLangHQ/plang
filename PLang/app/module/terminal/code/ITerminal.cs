using app.module.code;

namespace app.module.terminal.code;

/// <summary>
/// Terminal provider: spawns a program the program type has found and permitted (<see cref="type.program.@this"/>) —
/// held to its permissions when it has some, with a pipe pair when it asks, ending with this plang. What it does
/// once running is the process type's. Swappable via app.Code (a test can run programs without real processes).
/// </summary>
public interface ITerminal : ICode
{
    /// <summary>The program started, running; or why it isn't.</summary>
    Task<data.@this<type.process.@this>> Start(type.program.@this program, global::app.actor.context.@this context);
}
