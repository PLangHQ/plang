using System.Diagnostics;
using app.Attributes;
using app.error;

namespace app.test;

/// <summary>
/// One test — a <c>*.test.goal</c> file across its whole lifecycle. Born at
/// discovery (test.discover) carrying identity + discovery status; it runs itself
/// (<see cref="Start"/>) and holds its outcome (Status, Stdout, Timings, Error,
/// Duration). There is no separate execution record — the test IS its own run. Fields are plang values marked <c>[Out]</c>, so the test
/// rides the wire directly (the report serializes it — no hand-mapped shape).
/// The PLang name "test" derives from the @this namespace tail — no [PlangType] needed.
/// </summary>
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    global::app.type.item.IMatch<@this>, global::app.type.item.ICurrent<@this>,
    global::app.type.item.IList<@this, list.@this>
{
    private Stopwatch? _stopwatch;

    /// <summary>A key names this test by its goal's address — every test goal is named Start, so the
    /// address is what tells them apart.</summary>
    public async System.Threading.Tasks.ValueTask<@this?> Match(string key)
        => await Goal.Match(key) != null ? this : null;

    /// <summary>The test the asker's actor is running — the one its open session holds; null outside a test.</summary>
    public static @this? Current(global::app.actor.context.@this context)
        => (context.Actor?.Channel[typeof(global::app.channel.type.test.@this)] as global::app.channel.type.test.@this)?.Test;

    /// <summary>The run's tests.</summary>
    public static list.@this List(global::app.@this app) => new(app);

    public @this()
    {
        Tags = new global::app.type.item.list.@this<global::app.type.item.tag.@this>();
        Timings = new global::app.type.item.list.@this<global::app.test.timing.@this>();
    }

    // --- Discovery (populated by test.discover) ---

    /// <summary>The discovered goal. Always populated — built from the .pr
    /// when available, otherwise parsed from the .goal source itself. A report names it
    /// (<see cref="Name"/>, <see cref="Path"/>, <see cref="Hash"/>): the program lives in its .pr, and
    /// writing it out would render its %variables% in the runner's context.</summary>
    public required global::app.goal.@this Goal { get; init; }

    /// <summary>The test's goal, by name — the report's reference to it.</summary>
    [Out] public string Name => Goal.Name;

    /// <summary>The test's goal file.</summary>
    [Out] public global::app.type.item.path.@this? Path => Goal.Path;

    /// <summary>The hash of the goal text its .pr was built from — correlates a result with a build.</summary>
    [Out] public string? Hash => Goal.Hash;

    /// <summary>Lifecycle status: Ready/Stale/Skipped after discovery,
    /// Pass/Fail/Timeout after execution. A closed set — stays a C# enum, rides
    /// the wire as its name (like <c>Goal.Visibility</c>).</summary>
    [Out] public Status Status { get; set; } = Status.Ready;

    /// <summary>Human-readable reason for a non-Ready discovery status (e.g., "no .pr", "rebuild needed").</summary>
    [Out] public global::app.type.item.text.@this? StatusReason { get; set; }

    /// <summary>The test's tags: the goal's own (test.tag) ∪ the capabilities its reached
    /// actions require. Set by <see cref="app.test.list.@this.Create"/>.</summary>
    [Out] public global::app.type.item.list.@this<global::app.type.item.tag.@this> Tags { get; }

    // --- Execution (empty until the test runs) ---

    /// <summary>Wall-clock from <see cref="Begin"/> to <see cref="Complete(Status, global::app.error.Error?)"/>. Zero until the test runs.</summary>
    [Out] public global::app.type.item.duration.@this Duration { get; private set; } = System.TimeSpan.Zero;

    /// <summary>Error captured on fail/error. Carries AssertionError.Variables on assertion failures.</summary>
    [Out] public global::app.error.Error? Error { get; private set; }

    /// <summary>Text produced via the output channel during execution. Rendered on failure
    /// when verbose is off. Named Stdout (not Output) — Output is the wire-write method.</summary>
    [Out] public global::app.type.item.text.@this? Stdout { get; set; }

    /// <summary>Per-step wall-clock for the entry goal's top-level steps, in source order.</summary>
    [Out] public global::app.type.item.list.@this<global::app.test.timing.@this> Timings { get; }

    /// <summary>
    /// Runs this test's goal in <paramref name="app"/>, its own App (testing it), under the timeout test's
    /// setting says (≤ 0: none): the timeout rides the App's cancellation, so every action reading its
    /// context's token (timer.sleep, http.request, …) honours it. Its App's coverage records what fires;
    /// the test records the time each of its goal's own steps takes (a sub-goal's steps roll up into the
    /// step that called it) and what it wrote. Its outcome is its status — a crash inside its App is a
    /// Fail, never the caller's.
    /// </summary>
    public async System.Threading.Tasks.Task Start(global::app.@this app, global::app.actor.context.@this context)
    {
        var own = app.User.Context;
        Begin();
        app.test.list.Report.Coverage.Watch(own);
        Time(own);

        var seconds = context.Setting.Of<global::app.test.setting.@this>().TimeoutSeconds.ToDouble();
        using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        cts.CancelAfter(seconds <= 0 ? System.Threading.Timeout.InfiniteTimeSpan : System.TimeSpan.FromSeconds(seconds));
        own.PushCancellation(cts);
        var timedOut = () => cts.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested;
        try
        {
            var loaded = await app.goal.list.Load(Goal.PrPath!.ToString());
            var result = loaded.Success
                ? await ((await loaded.Value()) as global::app.goal.@this)!.Start(own)
                : loaded;
            if (timedOut()) Complete(Status.Timeout);
            else Complete(result);
        }
        catch (System.OperationCanceledException) when (timedOut())
        {
            Complete(Status.Timeout);
        }
        catch (System.Exception ex) when (ex is not (System.OutOfMemoryException or System.StackOverflowException))
        {
            Complete(Status.Fail, new ServiceError(ex.Message, "TestRunError", 500) { Exception = ex });
        }
        finally
        {
            own.PopCancellation();
            Stdout = app.test.list.Session?.Text;
        }
    }

    // Times each of this test's goal's own steps into Timings, from the step's start to its end.
    private void Time(global::app.actor.context.@this context)
    {
        var starts = new System.Collections.Concurrent.ConcurrentDictionary<int, long>();
        var entry = Goal.Path?.ToString();
        bool Own(global::app.goal.step.@this step) => string.Equals(step.Goal.Path?.ToString(), entry, System.StringComparison.Ordinal);
        context.Events.Register(new global::app.@event.lifecycle.binding.@this(
            global::app.@event.Trigger.BeforeStep,
            (ctx, _, _) =>
            {
                if (ctx.Step is { } step && Own(step)) starts[step.Index] = Stopwatch.GetTimestamp();
                return System.Threading.Tasks.Task.FromResult(ctx.Ok());
            },
            priority: int.MaxValue,
            stopOnError: false));
        context.Events.Register(new global::app.@event.lifecycle.binding.@this(
            global::app.@event.Trigger.AfterStep,
            (ctx, _, _) =>
            {
                if (ctx.Step is { } step && Own(step) && starts.TryRemove(step.Index, out var start))
                    Timings.Add(new global::app.test.timing.@this { Step = step, Elapsed = Stopwatch.GetElapsedTime(start) });
                return System.Threading.Tasks.Task.FromResult(ctx.Ok());
            },
            priority: int.MaxValue,
            stopOnError: false));
    }

    /// <summary>
    /// Why this test failed, as the console shows it — an assertion's expected and actual and the
    /// variables it captured, else the error's message — then what the test wrote, its ANSI escapes
    /// stripped so a test can't forge the runner's own output. Empty unless it failed with an error.
    /// </summary>
    public string Failure(global::app.actor.context.@this context)
    {
        if (Status != Status.Fail || Error == null) return "";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("    FAIL: " + Goal.Path);
        if (Error is AssertionError assert)
        {
            sb.AppendLine($"      Expected: {(global::app.Diagnostics.Format.Value(assert.Expected))}");
            sb.AppendLine($"      Actual:   {(global::app.Diagnostics.Format.Value(assert.Actual))}");
            if (assert.Variables is { CountRaw: > 0 } variables)
            {
                sb.AppendLine("      Variables:");
                foreach (var variable in variables.Entries(context))
                    sb.AppendLine($"        %{variable.Name}% = {(global::app.Diagnostics.Format.Value(variable.HasValue ? variable.Peek() : null))}");
            }
        }
        else
            sb.AppendLine($"      Error: {Error.Message}");
        var output = Stdout?.Clr<string>();
        if (!string.IsNullOrEmpty(output))
        {
            sb.AppendLine("      Output:");
            foreach (var line in Ansi.Replace(output, "").Split('\n'))
                sb.AppendLine("        " + line);
        }
        return sb.ToString();
    }

    private static readonly System.Text.RegularExpressions.Regex Ansi =
        new(@"\x1B\[[0-?]*[ -/]*[@-~]", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Self-write: a test is a structural item — its tagged [Out] fields ride the
    /// wire (the report serializes it), no hand-mapped shape.</summary>
    public override System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
        => OutputTagged(writer, mode, context);

    // --- Execution transitions ---

    /// <summary>Begins timing — the test's run begins.</summary>
    public void Begin() => _stopwatch = Stopwatch.StartNew();

    /// <summary>Transitions to the given terminal status and records elapsed duration.</summary>
    public void Complete(Status status, global::app.error.Error? error = null)
    {
        if (_stopwatch is { IsRunning: true }) _stopwatch.Stop();
        Duration = _stopwatch?.Elapsed ?? System.TimeSpan.Zero;
        Status = status;
        Error = error;
    }

    /// <summary>
    /// Completes based on a Data result: success → Pass; failure → Fail carrying the error.
    /// Skipped/Stale/Timeout have dedicated <see cref="Complete(Status, global::app.error.Error?)"/> calls.
    /// </summary>
    public void Complete(data.@this result)
    {
        if (result.Success) Complete(Status.Pass);
        else Complete(Status.Fail, result.Error);
    }
}
