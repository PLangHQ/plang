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

    /// <summary>What the program may touch — <c>[{path: "/src/os", verbs: ["read", "write"]}]</c>: the kernel holds it
    /// to these folders and verbs, and nothing else. Each is first this app's to grant. Left out, the program runs free;
    /// named, it never does (a %ref% holding none is refused).</summary>
    public partial data.@this<global::app.type.item.list.@this<global::app.type.item.permission.@this>>? Permission { get; init; }

    /// <summary>Goal called for each line the program writes to stdout, the line as <c>%!data%</c>.</summary>
    public partial data.@this<global::app.goal.step.action.@this>? OnOutput { get; init; }

    /// <summary>When true, stdout is binary messages, not text lines: each is [u32 length, little-endian]
    /// followed by that many bytes, and OnOutput gets the bytes as a binary <c>%!data%</c>.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Binary { get; init; }

    /// <summary>Each binary message goes straight to this screen (from <c>screen.open</c>) — no goal per
    /// message; output to a screen is binary. OnOutput, when given too, still gets each one.</summary>
    public partial data.@this<global::app.module.screen.type.screen.@this>? OutputTo { get; init; }

    /// <summary>Goal called for each line the program writes to stderr, the line as <c>%!data%</c>.</summary>
    public partial data.@this<global::app.goal.step.action.@this>? OnError { get; init; }

    /// <summary>When true, the program gets a pipe pair beside its standard streams — it reads fd 3 and writes fd 4 (what
    /// Chromium's <c>--remote-debugging-pipe</c> speaks DevTools on) — reached as <c>%program.pipe%</c>, a channel of the
    /// actor that started it. Linux only.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Pipe { get; init; }

    /// <summary>When true ("with a clean environment"), the program starts with none of plang's environment (its keys
    /// are there): only PATH and LANG, what <see cref="Keep"/> names and what <see cref="Environment"/> gives. The
    /// terminal's environment setting is not taken.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Clean { get; init; }

    /// <summary>Names of variables copied from plang's own environment into a clean one ("keep PULSE_SERVER, HOME") —
    /// names only, never values.</summary>
    public partial data.@this<global::app.type.item.list.@this>? Keep { get; init; }

    [Code]
    public partial ITerminal Terminal { get; }

    public async Task<data.@this<Process>> Start() => await Terminal.Open(this);
}
