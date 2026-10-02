using app.Attributes;
using app.module.terminal.code;

namespace app.module.terminal;

/// <summary>
/// Starts a program and keeps it running — the step returns at once with the running program. Each
/// line it writes to stdout calls OnOutput, each line on stderr calls OnError, with the line as
/// <c>%!data%</c>. Talk to it with <c>terminal.send</c>; <c>terminal.wait</c> waits for it to exit.
/// </summary>
[Action("open", Cacheable = false)]
[RequiresCapability("process")]
public partial class open : IContext
{
    /// <summary>The program: a name found on the OS PATH (<c>wsl.exe</c>) or a path.</summary>
    public partial data.@this<global::app.type.item.text.@this> App { get; init; }

    /// <summary>The program's arguments, in order — each one passed as-is, never through a shell.</summary>
    public partial data.@this<global::app.type.item.list.@this>? Parameter { get; init; }

    /// <summary>Environment variables for this run, merged over <c>%!terminal.environment%</c>.</summary>
    public partial data.@this<global::app.type.item.dict.@this>? Environment { get; init; }

    /// <summary>Folder the program runs in. Default: the app root.</summary>
    public partial data.@this<global::app.type.item.path.@this>? WorkingDirectory { get; init; }

    /// <summary>The folders the program is held to — <c>{read: [...], write: [...]}</c>: the kernel lets it touch
    /// nothing else. Each folder is first this app's to read or write. No jail, no hold.</summary>
    public partial data.@this<global::app.module.terminal.type.jail.@this>? Jail { get; init; }

    /// <summary>Goal called for each line the program writes to stdout, the line as <c>%!data%</c>.</summary>
    public partial data.@this<global::app.goal.step.action.@this>? OnOutput { get; init; }

    /// <summary>When true, stdout is binary messages, not text lines: each is [u32 length, little-endian]
    /// followed by that many bytes, and OnOutput gets the bytes as a binary <c>%!data%</c>.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Binary { get; init; }

    /// <summary>Each binary message goes straight to this screen (from <c>screen.open</c>) — no goal per
    /// message; output to a screen is binary. OnOutput, when given too, still gets each one.</summary>
    public partial data.@this<global::app.module.screen.Screen>? OutputTo { get; init; }

    /// <summary>Goal called for each line the program writes to stderr, the line as <c>%!data%</c>.</summary>
    public partial data.@this<global::app.goal.step.action.@this>? OnError { get; init; }

    [Code]
    public partial ITerminal Terminal { get; }

    public async Task<data.@this<Process>> Start() => await Terminal.Open(this);
}
