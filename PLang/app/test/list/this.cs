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
    private readonly global::app.type.list.@this<global::app.test.@this> _tests;
    private readonly object _lock = new();
    private readonly actor.context.@this _context;

    public @this(actor.context.@this context)
    {
        _context = context;
        _tests = new global::app.type.list.@this<global::app.test.@this>(context);
        Include = new global::app.type.list.@this<global::app.type.item.text.@this>(context);
        Exclude = new global::app.type.list.@this<global::app.type.item.text.@this>(context);
    }

    /// <summary>When the session started.</summary>
    public global::app.type.datetime.@this StartedAt { get; } = new(System.DateTime.UtcNow);

    /// <summary>Run-wide coverage tracker. Per-test child Apps populate their own, then Merge into the parent here.</summary>
    public Coverage Coverage { get; } = new();

    /// <summary>The test currently in flight on this App. null when no test is running. test.run assigns; test.tag reads.</summary>
    public global::app.test.@this? Current { get; internal set; }

    // --- Configuration (plang values) ---

    /// <summary>Per-test wall-clock timeout in seconds. Default 30.</summary>
    public global::app.type.number.@this TimeoutSeconds { get; set; } = 30;

    /// <summary>Parallelism bound for test.run's semaphore. Default Environment.ProcessorCount.</summary>
    public global::app.type.number.@this Parallel { get; set; } = System.Environment.ProcessorCount;

    /// <summary>When true, per-test output.write streams live to stdout. When false, captured and rendered only on failure.</summary>
    public global::app.type.item.@bool.@this Verbose { get; set; } = false;

    /// <summary>File report format. Console is always written regardless.</summary>
    public global::app.type.item.choice.@this<global::app.test.Format> Format { get; set; } = global::app.test.Format.Json;

    /// <summary>Include tag filter (empty = all tests match). Case-insensitive. Set by the
    /// <c>--test</c> walk (<c>["a","b"]</c> → <c>list&lt;text&gt;</c> via the catalog).</summary>
    public global::app.type.list.@this<global::app.type.item.text.@this> Include { get; set; }

    /// <summary>Exclude tag filter (empty = nothing excluded). Applied after include — exclude wins on conflict.</summary>
    public global::app.type.list.@this<global::app.type.item.text.@this> Exclude { get; set; }

    /// <summary>Back-reference to the App that owns this session (derived from the born
    /// context). Used by reporters to surface App.Version for drift comparisons.</summary>
    internal app.@this App => _context.App;

    /// <summary>The context this session was born with (system-scoped).</summary>
    private actor.context.@this Context => _context;

    // --- The tests (the collection this session owns) ---

    /// <summary>Number of tests recorded.</summary>
    public int Count => _tests.Count.ToInt32();

    /// <summary>Records a processed test. Thread-safe — parallel child Apps add concurrently
    /// (the session owns the lock; the plang list is not itself concurrent).</summary>
    public void Add(global::app.test.@this test)
    {
        lock (_lock) _tests.Add(test);
    }

    /// <summary>The recorded tests, materialized (each row's value is a live test reference).</summary>
    public IReadOnlyList<global::app.test.@this> Tests
    {
        get { lock (_lock) return _tests.Select(r => (global::app.test.@this)r.Peek()).ToList(); }
    }

    /// <summary>The tests as a plang <c>list&lt;test&gt;</c> (the wire/return shape).</summary>
    public global::app.type.list.@this<global::app.test.@this> TestList { get { lock (_lock) return _tests; } }

    /// <summary>Per-status counts across all recorded tests. Every status key is present, even with count 0.</summary>
    public Dictionary<Status, int> Summary() => Summary(Tests);

    /// <summary>Per-status counts across a given set of tests. Every status key present, even at 0.</summary>
    public static Dictionary<Status, int> Summary(IEnumerable<global::app.test.@this> tests)
    {
        var summary = new Dictionary<Status, int>();
        foreach (Status status in System.Enum.GetValues<Status>())
            summary[status] = 0;
        foreach (var test in tests)
            summary[test.Status]++;
        return summary;
    }

    // No Apply(--test={...}) — the setting walk (app.Setting.Set(app.Test, dict)) sets the
    // config leaves directly: TimeoutSeconds/Parallel (number), Verbose (@bool), Format
    // (choice<Format> — unknown value rejected by the conversion), Include/Exclude
    // (list<text>, converted element-wise). Bounds are sentinels, not errors: test.run reads
    // TimeoutSeconds ≤ 0 as no-timeout and Parallel ≤ 0 as auto (ProcessorCount).
}
