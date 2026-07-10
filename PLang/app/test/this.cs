using System.Diagnostics;
using app.Attributes;
using app.error;

namespace app.test;

/// <summary>
/// One test — a <c>*.test.goal</c> file across its whole lifecycle. Born at
/// discovery (test.discover) carrying identity + discovery status; executed by
/// test.run, which stamps the execution outcome (Status, Output, Timings, Error,
/// Duration) onto the same instance. There is no separate execution record — the
/// test IS its own run. Fields are plang values marked <c>[Out]</c>, so the test
/// rides the wire directly (the report serializes it — no hand-mapped shape).
/// The PLang name "test" derives from the @this namespace tail — no [PlangType] needed.
/// </summary>
public sealed class @this : global::app.type.item.@this
{
    private Stopwatch? _stopwatch;

    public @this(actor.context.@this context)
    {
        Tags = new global::app.type.list.@this<global::app.type.item.text.@this>(context);
        Timings = new global::app.type.list.@this<global::app.test.timing.@this>(context);
    }

    // --- Discovery (populated by test.discover) ---

    /// <summary>The discovered goal. Always populated — built from the .pr
    /// when available, otherwise parsed from the .goal source itself.</summary>
    [Out] public required global::app.goal.@this Goal { get; init; }

    /// <summary>Lifecycle status: Ready/Stale/Skipped after discovery,
    /// Pass/Fail/Timeout after execution. A closed set — stays a C# enum, rides
    /// the wire as its name (like <c>Goal.Visibility</c>).</summary>
    [Out] public Status Status { get; set; } = Status.Ready;

    /// <summary>Human-readable reason for a non-Ready discovery status (e.g., "no .pr", "rebuild needed").</summary>
    [Out] public global::app.type.item.text.@this? StatusReason { get; set; }

    /// <summary>Tags (case-insensitive): user-declared (test.tag) ∪ auto (handler
    /// [RequiresCapability]), discovery-seeded and run-appended into one set.</summary>
    [Out] public global::app.type.list.@this<global::app.type.item.text.@this> Tags { get; }

    // --- Execution (stamped by test.run; empty until the test runs) ---

    /// <summary>Wall-clock from <see cref="Start"/> to <see cref="Complete(Status, IError?)"/>. Zero until the test runs.</summary>
    [Out] public global::app.type.item.duration.@this Duration { get; private set; } = System.TimeSpan.Zero;

    /// <summary>Error captured on fail/error. Carries AssertionError.Variables on assertion failures.</summary>
    [Out] public IError? Error { get; private set; }

    /// <summary>Text produced via the output channel during execution. Rendered on failure
    /// when verbose is off. Named Stdout (not Output) — Output is the wire-write method.</summary>
    [Out] public global::app.type.item.text.@this? Stdout { get; set; }

    /// <summary>Per-step wall-clock for the entry goal's top-level steps, in source order.</summary>
    [Out] public global::app.type.list.@this<global::app.test.timing.@this> Timings { get; }

    /// <summary>Self-write: a test is a structural item — its tagged [Out] fields ride the
    /// wire (the report serializes it), no hand-mapped shape.</summary>
    public override System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
        => OutputTagged(writer, mode, context);

    // --- Execution transitions ---

    /// <summary>Begins timing — called by test.run when execution starts.</summary>
    public void Start() => _stopwatch = Stopwatch.StartNew();

    /// <summary>Transitions to the given terminal status and records elapsed duration.</summary>
    public void Complete(Status status, IError? error = null)
    {
        if (_stopwatch is { IsRunning: true }) _stopwatch.Stop();
        Duration = _stopwatch?.Elapsed ?? System.TimeSpan.Zero;
        Status = status;
        Error = error;
    }

    /// <summary>
    /// Completes based on a Data result: success → Pass; failure → Fail carrying the error.
    /// Skipped/Stale/Timeout have dedicated <see cref="Complete(Status, IError?)"/> calls.
    /// </summary>
    public void Complete(data.@this result)
    {
        if (result.Success) Complete(Status.Pass);
        else Complete(Status.Fail, result.Error);
    }
}
