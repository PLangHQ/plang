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

    /// <summary>The operating system's process it is.</summary>
    internal code.child.@this? Os { get; set; }

    /// <summary>Its pipe pair (<c>terminal.open … with pipe</c>) as a channel: what is written goes to its fd 3, what it
    /// writes on its fd 4 is read — each message ended as the channel's end says (a newline unless set: DevTools'
    /// pipe ends each with NUL). Null when it was started without one.</summary>
    [LlmBuilder, Out] public global::app.channel.type.stream.@this? pipe { get; internal set; }

    /// <summary>The OS process, handed to the step that started it to run it to its end (<c>terminal.start</c>): that
    /// step owns it from here and disposes it; this item holds it no longer.</summary>
    internal code.child.@this Take()
    {
        var os = Os ?? throw new InvalidOperationException($"{Program} has no process to take");
        Os = null;
        return os;
    }

    /// <summary>It speaks plang's own format — a plang started with <c>--app.type.format=application/plang</c>: what it
    /// writes arrives as Data (an ask as an Ask), and what is sent to it goes as Data, signed.</summary>
    internal bool Plang { get; init; }
    internal SemaphoreSlim Writing { get; } = new(1, 1);
    internal Task? Reading { get; set; }

    // its last words on stderr — what it said before it stopped, if it stops
    private readonly Queue<string> _said = new();

    /// <summary>A line it wrote on stderr: the last 40 are kept.</summary>
    internal void Heard(string line)
    {
        lock (_said)
        {
            _said.Enqueue(line);
            while (_said.Count > 40) _said.Dequeue();
        }
    }

    /// <summary>What it said last on stderr (up to 40 lines), oldest first.</summary>
    internal string Said
    {
        get { lock (_said) return string.Join("\n", _said); }
    }

    /// <summary>The app this program runs, when it is a plang that takes calls on its input (PlangOS): its goals are
    /// called like this app's — <c>call goal Question in %container%</c>, <c>%container.goal["Question"]%</c> — the call
    /// going down its input, the answer coming back beside its frames. The same link as <c>%!app.parent%</c>, the
    /// other way.</summary>
    internal global::app.parent.@this Remote { get; } = new();

    /// <summary>One step by dot: <c>goal</c> — the goals of the app it runs; any other member as every item's.</summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => string.Equals(key, "goal", StringComparison.OrdinalIgnoreCase) ? Remote.Get(parent, key) : base.Get(parent, key);

    public override string ToString() => $"{Program} (pid {Id}{(Running ? "" : ", exited")})";
}
