using app.module.code;

namespace app.module.terminal.code;

/// <summary>
/// Terminal provider: spawns a program the program type has found and permitted (<see cref="type.program.@this"/>) —
/// held to its permissions when it has some, with a pipe pair when it asks, ending with this plang. What it does
/// once running is the process type's. Swappable via app.Code (a test can run programs without real processes).
/// </summary>
public interface ITerminal : ICode
{
    /// <summary>The program a bare name (<c>git</c>, <c>wsl.exe</c>) names on this machine, found the way a shell finds
    /// it — on <c>PATH</c>, each <c>PATHEXT</c> extension tried on Windows when the name has none; null when none is.
    /// Finding is not permission: starting it is still authorized.</summary>
    global::app.type.item.path.file.@this? Find(string name, global::app.actor.context.@this context);

    /// <summary>The program started, running; or why it isn't.</summary>
    Task<data.@this<type.process.@this>> Start(type.program.@this program, global::app.actor.context.@this context);
}
