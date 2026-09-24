using app.error;
using app.test;

namespace app.test.list;

/// <summary>
/// The test session — the collection of tests for one run, plus run-wide state:
/// start time, coverage, config, and the per-status tallies. Reached at
/// <c>app.Test</c>. When Test is active (present, born under --test), downstream systems observe it
/// (in-memory DBs, stub identity, etc.) and test.tag / assert handlers use the
/// state here. Activated by: plang --test. Config/state are plang values.
/// </summary>
public sealed partial class @this
{
    private readonly global::app.type.item.list.@this<global::app.test.@this> _tests;
    private readonly actor.context.@this _context;

    public @this(actor.context.@this context)
    {
        _context = context;
        _tests = new global::app.type.item.list.@this<global::app.test.@this>();
        Include = new global::app.type.item.list.@this<global::app.type.item.text.@this>();
        Exclude = new global::app.type.item.list.@this<global::app.type.item.text.@this>();
    }

    /// <summary>When the session started.</summary>
    public global::app.type.item.datetime.@this StartedAt { get; } = new(System.DateTime.UtcNow);

    /// <summary>Run-wide coverage tracker. Per-test child Apps populate their own, then Merge into the parent here.</summary>
    public Coverage Coverage { get; } = new();

    /// <summary>The test currently in flight on this App. null when no test is running. test.run assigns; test.tag reads.</summary>
    public global::app.test.@this? Current { get; internal set; }

    // --- Configuration (plang values) ---

    /// <summary>Per-test wall-clock timeout in seconds. Default 30.</summary>
    public global::app.type.item.number.@this TimeoutSeconds { get; set; } = 30;

    /// <summary>Parallelism bound for test.run's semaphore. Default Environment.ProcessorCount.</summary>
    public global::app.type.item.number.@this Parallel { get; set; } = System.Environment.ProcessorCount;

    /// <summary>When true, per-test output.write streams live to stdout. When false, captured and rendered only on failure.</summary>
    public global::app.type.item.@bool.@this Verbose { get; set; } = false;

    /// <summary>File report format. Console is always written regardless.</summary>
    public global::app.type.item.choice.@this<global::app.test.Format> Format { get; set; } = global::app.test.Format.Json;

    /// <summary>Include tag filter (empty = all tests match). Case-insensitive. Set by the
    /// <c>--test</c> walk (<c>["a","b"]</c> → <c>list&lt;text&gt;</c> via the catalog).</summary>
    public global::app.type.item.list.@this<global::app.type.item.text.@this> Include { get; set; }

    /// <summary>Exclude tag filter (empty = nothing excluded). Applied after include — exclude wins on conflict.</summary>
    public global::app.type.item.list.@this<global::app.type.item.text.@this> Exclude { get; set; }

    /// <summary>The test a test goal is, as this run takes it. Its tags are the goal's own
    /// (<c>goal.Tag</c>, stamped at build) and the capabilities every action it reaches requires —
    /// its own and those of each goal its calls reach, as they are now. Seeds this run's coverage with
    /// the same goals. A goal tagged <c>skip</c> is Skipped; a test this run's tag filter leaves out
    /// is Skipped with the filter's reason; otherwise Ready.</summary>
    public async Task<global::app.test.@this> Create(global::app.goal.@this goal, actor.context.@this context)
    {
        var test = new global::app.test.@this { Goal = goal };
        test.Tags.Add(goal.Tag);

        var reached = new List<global::app.goal.@this> { goal };
        reached.AddRange(await goal.Callee(context));
        foreach (var g in reached)
        {
            foreach (var step in g.Step.Items())
                foreach (var action in step.Action.Items())
                    foreach (var required in action.Requirement)
                        if (global::app.type.item.tag.@this.Create(required) is { } tag) test.Tags.Add(tag);
            Coverage.Add(g);
        }

        if (goal.Tag.Has(new global::app.type.item.tag.@this("skip")))
        {
            test.Status = Status.Skipped;
            test.StatusReason = "tagged 'skip'";
        }
        else if (Exclusion(test) is { } reason)
        {
            test.Status = Status.Skipped;
            test.StatusReason = reason;
        }
        return test;
    }

    /// <summary>Why this run leaves <paramref name="test"/> out — a tag in <see cref="Exclude"/> (exclude
    /// wins), or no tag in a non-empty <see cref="Include"/>. Null when the run takes it. Tags compare
    /// by the tag's own equality.</summary>
    public global::app.type.item.text.@this? Exclusion(global::app.test.@this test)
    {
        var tags = test.Tags.Items().ToHashSet();
        bool Carries(global::app.type.item.list.@this<global::app.type.item.text.@this> filter)
            => filter.Items().Any(t => global::app.type.item.tag.@this.Create(t) is { } tag && tags.Contains(tag));

        if (Exclude.CountRaw > 0 && Carries(Exclude)) return "excluded by tag";
        if (Include.CountRaw > 0 && !Carries(Include)) return "no include match";
        return null;
    }

    /// <summary>Back-reference to the App that owns this session (derived from the born
    /// context). Used by reporters to surface App.Version for drift comparisons.</summary>
    internal app.@this App => _context.App;

    /// <summary>The context this session was born with (system-scoped).</summary>
    private actor.context.@this Context => _context;

    // --- The tests (the collection this session owns) ---

    /// <summary>Number of tests recorded.</summary>
    public int Count => _tests.Count.ToInt32();

    /// <summary>Records a processed test. Parallel child Apps add concurrently — the list guards
    /// its own rows.</summary>
    public void Add(global::app.test.@this test) => _tests.Add(test);

    /// <summary>The recorded tests, materialized (each row's value is a live test reference).</summary>
    public IReadOnlyList<global::app.test.@this> Tests => _tests.Items().ToList();

    /// <summary>Per-status counts across the recorded tests. Every status key is present, even with count 0.</summary>
    public Dictionary<Status, int> Summary()
    {
        var summary = new Dictionary<Status, int>();
        foreach (Status status in System.Enum.GetValues<Status>())
            summary[status] = 0;
        foreach (var test in Tests)
            summary[test.Status]++;
        return summary;
    }

    /// <summary>Why this run did not pass, or null when it did — tests ran and each passed or was
    /// deliberately skipped. It fails when nothing was discovered, when a test could not load (grouped
    /// by reason: "12 tests could not load: old .pr format … — rebuild it."), when a test never ran,
    /// and when a test failed or timed out.</summary>
    public Error? Verdict()
    {
        var tests = Tests;
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

    // No Apply(--test={...}) — the setting walk (app.Setting.Set(app.Test, dict)) sets the
    // config leaves directly: TimeoutSeconds/Parallel (number), Verbose (@bool), Format
    // (choice<Format> — unknown value rejected by the conversion), Include/Exclude
    // (list<text>, converted element-wise). Bounds are sentinels, not errors: test.run reads
    // TimeoutSeconds ≤ 0 as no-timeout and Parallel ≤ 0 as auto (ProcessorCount).
}
