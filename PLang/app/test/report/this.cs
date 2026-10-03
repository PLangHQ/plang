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
    /// unless the run is nested in a test, whose verdict it is — and the artefact at the app root, in the
    /// format test's setting says. The json artefact is the tests' own wire form. Answers the tests, the artefact's facts on its Properties
    /// (format, reportPath, content, the summary counts); a top-level run that didn't pass answers its
    /// verdict — after the artefact is written.
    /// </summary>
    public async Task<global::app.data.@this> Write(global::app.actor.context.@this context)
    {
        var tests = _tests.Items().ToList();
        var chosen = context.Setting.Of<global::app.test.setting.@this>().Format.Value;
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
                console.Append(await test.Failure(context));
            }
            // the app's own goals under test — each file's public goal and its private ones; test files test them
            var goals = new List<global::app.goal.@this>();
            await foreach (var file in context.App.goal.list.Walk(new global::app.type.item.dict.@this().Set("os", false), context))
                if (!file.IsTest) goals.AddRange([file, .. file.Child.Items()]);
            console.Append(Coverage.Text(context.App.module.list, goals));
            // the channel ends the line
            await context.Actor.Channel[global::app.channel.list.@this.Output].WriteText(console.ToString().TrimEnd('\r', '\n'));
        }

        // the run written by the chosen format's kind, its Out face, to the file that kind writes a report to
        var run = new global::app.type.item.list.@this<global::app.test.@this>(tests);
        using var ms = new System.IO.MemoryStream();
        var encoded = await chosen.Kind.Encode(ms, context.Ok(run), context, global::app.View.Out);
        if (!encoded.Success) return encoded;
        var content = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        var target = global::app.type.item.path.@this.Resolve("/.test/" + chosen.Kind.Report, context);
        var written = await target.WriteText(content, context);
        if (!written.Success) return context.Error(written.Error!);

        var result = context.Ok<global::app.type.item.list.@this<global::app.test.@this>>(run);
        result.Property.Set("format", chosen.ToString());
        result.Property.Set("reportPath", target.Absolute);
        result.Property.Set("content", content);
        result.Property.Set("summaryTotal", tests.Count);
        result.Property.Set("summaryPass", summary[Status.Pass]);
        result.Property.Set("summaryFail", summary[Status.Fail]);
        result.Property.Set("variableSnapshotCount", tests.Count(t => t.Error?.Variables is { CountRaw: > 0 }));

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
