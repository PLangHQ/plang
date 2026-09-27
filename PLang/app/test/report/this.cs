using app.error;

namespace app.test.report;

/// <summary>
/// What a test run comes to — when it started, what it covered, how many tests ended how, and whether it
/// passed. Reads the run's tests off the list it was born with; <c>test.report</c> writes it out.
/// </summary>
public sealed class @this
{
    private readonly global::app.test.list.@this _tests;

    public @this(global::app.test.list.@this tests) => _tests = tests;

    /// <summary>When the run started.</summary>
    public global::app.type.item.datetime.@this StartedAt { get; } = new(System.DateTime.UtcNow);

    /// <summary>The run's coverage. Each test's App records its own, then merges it in here.</summary>
    public Coverage Coverage { get; } = new();

    /// <summary>Per-status counts across the run's tests. Every status key is present, even with count 0.</summary>
    public Dictionary<Status, int> Summary()
    {
        var summary = new Dictionary<Status, int>();
        foreach (Status status in System.Enum.GetValues<Status>())
            summary[status] = 0;
        foreach (var test in _tests.Items())
            summary[test.Status]++;
        return summary;
    }

    /// <summary>
    /// Writes the run out: to the console its summary, each test (a failure with why) and the coverage —
    /// unless the run is nested in a test, whose verdict it is — and the artefact at the app root, in
    /// <paramref name="format"/> (<c>junit</c>, else json), else the format test's setting says. The json
    /// artefact is the tests' own wire form. Answers the tests, the artefact's facts on its Properties
    /// (format, reportPath, content, the summary counts); a top-level run that didn't pass answers its
    /// verdict — after the artefact is written.
    /// </summary>
    public async Task<global::app.data.@this> Write(global::app.type.item.text.@this? format,
        global::app.actor.context.@this context)
    {
        var tests = _tests.Items().ToList();
        var chosen = format == null ? (Format)context.Setting.Of<global::app.test.setting.@this>().Format
            : string.Equals(format.ToString(), "junit", System.StringComparison.OrdinalIgnoreCase) ? Format.JUnit : Format.Json;
        var nested = global::app.test.@this.Current(context) != null;
        var summary = Summary();

        if (!nested)
        {
            var console = new System.Text.StringBuilder();
            console.AppendLine($"Test summary: {tests.Count} total, "
                + $"{summary[Status.Pass]} pass, {summary[Status.Fail]} fail, "
                + $"{summary[Status.Timeout]} timeout, {summary[Status.Stale]} stale, "
                + $"{summary[Status.Skipped]} skipped");
            var version = _tests.App.Version;
            foreach (var test in tests)
            {
                var drift = !string.IsNullOrEmpty(test.Goal.BuilderVersion) && !string.IsNullOrEmpty(version)
                    && !string.Equals(test.Goal.BuilderVersion, version, System.StringComparison.Ordinal);
                console.AppendLine($"  [{test.Status}] {test.Goal.Path} ({test.Duration.TotalMilliseconds:F0}ms)"
                    + (drift ? " [builder drift]" : ""));
                console.Append(test.Failure(context));
            }
            console.Append(Coverage.Text(context.App.module.list));
            await context.Actor.Channel.WriteTextAsync(global::app.channel.list.@this.Output, console.ToString());
        }

        var run = new global::app.type.item.list.@this<global::app.test.@this>(tests);
        string content;
        global::app.type.item.path.@this target;
        if (chosen == Format.JUnit)
        {
            content = new global::app.test.junit.@this(tests).ToString();
            target = global::app.type.item.path.@this.Resolve("/.test/junit.xml", context);
        }
        else
        {
            var serializer = new global::app.channel.serializer.plang.@this(context);
            using var ms = new System.IO.MemoryStream();
            await serializer.SerializeItemAsync(ms, run, global::app.View.Out);
            content = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            target = global::app.type.item.path.@this.Resolve("/.test/results.json", context);
        }
        var written = await target.WriteText(content, context);
        if (!written.Success) return context.Error(written.Error!);

        var result = context.Ok<global::app.type.item.list.@this<global::app.test.@this>>(run);
        result.Properties.Set("format", chosen.ToString());
        result.Properties.Set("reportPath", target.Absolute);
        result.Properties.Set("content", content);
        result.Properties.Set("summaryTotal", tests.Count);
        result.Properties.Set("summaryPass", summary[Status.Pass]);
        result.Properties.Set("summaryFail", summary[Status.Fail]);
        result.Properties.Set("variableSnapshotCount", tests.Count(t => t.Error?.Variables is { CountRaw: > 0 }));

        if (!nested && Verdict() is { } failed) return context.Error(failed);
        return result;
    }

    /// <summary>Why this run did not pass, or null when it did — tests ran and each passed or was
    /// deliberately skipped. It fails when nothing was discovered, when a test could not load (grouped
    /// by reason: "12 tests could not load: old .pr format … — rebuild it."), when a test never ran,
    /// and when a test failed or timed out.</summary>
    public Error? Verdict()
    {
        var tests = _tests.Items().ToList();
        if (tests.Count == 0)
            return new Error("no tests were discovered — nothing ran.", "NoTestsDiscovered", 400);

        static string Counted(int count) => count == 1 ? "1 test" : $"{count} tests";
        var problems = new List<string>();
        foreach (var group in tests.Where(t => t.Status == Status.Stale)
                     .GroupBy(t => t.StatusReason?.ToString() ?? "unknown reason"))
            problems.Add($"{Counted(group.Count())} could not load: {group.Key.TrimEnd('.')}");
        if (tests.Count(t => t.Status == Status.Ready) is > 0 and var idle)
            problems.Add($"{Counted(idle)} did not run");
        if (tests.Count(t => t.Status is Status.Fail or Status.Timeout) is > 0 and var failed)
            problems.Add($"{Counted(failed)} failed");

        return problems.Count == 0 ? null
            : new Error(string.Join("; ", problems) + ".", "TestRunFailed", 400);
    }
}
