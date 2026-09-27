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
