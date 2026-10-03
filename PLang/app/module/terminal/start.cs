using app.Attributes;
using app.module.terminal.code;

namespace app.module.terminal;

/// <summary>
/// Starts a program on this machine and waits for it to finish. The result's value is what the
/// program wrote to stdout; <c>!exitCode</c>, <c>!error</c> (stderr), <c>!duration</c> and
/// <c>!program</c> ride as properties. A program outside the app root is started only after the
/// actor allows it (<c>execute</c>, y/n/a).
/// </summary>
[Action("start", Cacheable = false)]
[RequiresCapability("process")]
public partial class start : IContext
{
    /// <summary>The program: a name found on the OS PATH (<c>wsl.exe</c>, <c>git</c>) or a path.</summary>
    public partial data.@this<global::app.type.item.text.@this> App { get; init; }

    /// <summary>The program's arguments, in order — each one passed as-is, never through a shell.</summary>
    public partial data.@this<global::app.type.item.list.@this>? Parameter { get; init; }

    /// <summary>Environment variables for this run, merged over <c>%!terminal.environment%</c>.</summary>
    public partial data.@this<global::app.type.item.dict.@this>? Environment { get; init; }

    /// <summary>Folder the program runs in. Default: the app root.</summary>
    public partial data.@this<global::app.type.item.path.@this>? WorkingDirectory { get; init; }

    /// <summary>What the program may touch — <c>[{path: "/src/os", verbs: ["read", "write"]}]</c>: the kernel holds it
    /// to these folders and verbs, and nothing else. Each is first this app's to grant. Left out, the program runs free;
    /// named, it never does (a %ref% holding none is refused). Not with Administrator.</summary>
    public partial data.@this<global::app.type.item.list.@this<global::app.type.item.permission.@this>>? Permission { get; init; }

    /// <summary>When true ("with a clean environment"), the program starts with none of plang's environment (its keys
    /// are there): only PATH and LANG, what <see cref="Keep"/> names and what <see cref="Environment"/> gives. The
    /// terminal's environment setting is not taken.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Clean { get; init; }

    /// <summary>Names of variables copied from plang's own environment into a clean one ("keep PULSE_SERVER, HOME") —
    /// names only, never values.</summary>
    public partial data.@this<global::app.type.item.list.@this>? Keep { get; init; }

    /// <summary>Text written to the program's stdin, which is then closed.</summary>
    public partial data.@this<global::app.type.item.text.@this>? Input { get; init; }

    /// <summary>When true, the program takes over this console (its keyboard and screen) until it
    /// exits — for programs a person uses, like <c>wsl -d PlangOS</c>. Output is then not captured.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Interactive { get; init; }

    /// <summary>When true, starts the program with administrator rights (Windows asks through UAC).
    /// Output is then not captured; only the exit code comes back.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Administrator { get; init; }

    /// <summary>Goal called for each line the program writes to stdout, the line as <c>%!data%</c>.</summary>
    public partial data.@this<global::app.goal.step.action.@this>? OnOutput { get; init; }

    /// <summary>Goal called for each line the program writes to stderr, the line as <c>%!data%</c>.</summary>
    public partial data.@this<global::app.goal.step.action.@this>? OnError { get; init; }

    [Code]
    public partial ITerminal Terminal { get; }

    public async Task<data.@this<global::app.type.item.text.@this>> Start() => await Terminal.Start(this);
}
