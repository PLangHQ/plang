using app.error;
using app.test;

namespace app.test.list;

/// <summary>
/// The test session — the collection of tests for one run, plus run-wide state:
/// start time, coverage, config, and the per-status tallies. Reached at
/// <c>app.Test</c>. When IsEnabled is on, downstream systems observe it
/// (in-memory DBs, stub identity, etc.) and test.tag / assert handlers use the
/// state here. Activated by: plang --test. Config/state are plang values.
/// </summary>
public sealed partial class @this
{
    private readonly global::app.type.list.@this<global::app.test.@this> _tests;
    private global::app.type.list.@this<global::app.type.text.@this> _include;
    private global::app.type.list.@this<global::app.type.text.@this> _exclude;
    private readonly object _lock = new();
    private readonly actor.context.@this _context;

    public @this(actor.context.@this context)
    {
        _context = context;
        _tests = new global::app.type.list.@this<global::app.test.@this>(context);
        _include = new global::app.type.list.@this<global::app.type.text.@this>(context);
        _exclude = new global::app.type.list.@this<global::app.type.text.@this>(context);
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
    public global::app.type.@bool.@this Verbose { get; set; } = false;

    /// <summary>File report format. Console is always written regardless.</summary>
    public global::app.type.choice.@this<global::app.test.Format> Format { get; set; } = global::app.test.Format.Json;

    /// <summary>Include tag filter (empty = all tests match). Case-insensitive.</summary>
    public global::app.type.list.@this<global::app.type.text.@this> Include => _include;

    /// <summary>Exclude tag filter (empty = nothing excluded). Applied after include — exclude wins on conflict.</summary>
    public global::app.type.list.@this<global::app.type.text.@this> Exclude => _exclude;

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

    /// <summary>
    /// Applies a --test={...} config dictionary. Validates bounds and returns an error Data
    /// on invalid values (negative timeout, non-positive parallel, unknown format).
    /// On success, returns Data.Ok().
    /// </summary>
    public data.@this Apply(IDictionary<string, object?> config)
    {
        foreach (var kvp in config)
        {
            // The config dict comes from a parsed --test={...} JSON, so values ride
            // as scalar wrappers (number/bool/text). Unwrap each to its raw backing
            // once at the boundary; the bound-checks below read raw CLR unchanged.
            var value = kvp.Value is app.type.item.@this iv ? iv.Clr<object>() : kvp.Value;
            switch (kvp.Key.ToLowerInvariant())
            {
                case "timeout":
                case "timeoutseconds":
                {
                    if (!TryToInt(value, out var timeout))
                        return Context.Error(ConfigError($"--test.timeout must be an integer, got {Describe(value)}"));
                    if (timeout <= 0)
                        return Context.Error(ConfigError($"--test.timeout must be positive, got {timeout}"));
                    TimeoutSeconds = timeout;
                    break;
                }
                case "parallel":
                {
                    if (!TryToInt(value, out var parallel))
                        return Context.Error(ConfigError($"--test.parallel must be an integer, got {Describe(value)}"));
                    if (parallel <= 0)
                        return Context.Error(ConfigError($"--test.parallel must be positive, got {parallel}"));
                    Parallel = parallel;
                    break;
                }
                case "include":
                    _include = new global::app.type.list.@this<global::app.type.text.@this>(
                        ToStringList(value).Select(s => new global::app.type.text.@this(s)), Context);
                    break;
                case "exclude":
                    _exclude = new global::app.type.list.@this<global::app.type.text.@this>(
                        ToStringList(value).Select(s => new global::app.type.text.@this(s)), Context);
                    break;
                case "verbose":
                {
                    if (value is bool b) Verbose = b;
                    else if (value is string s && bool.TryParse(s, out var bs)) Verbose = bs;
                    else return Context.Error(ConfigError($"--test.verbose must be a boolean, got {Describe(value)}"));
                    break;
                }
                case "format":
                {
                    var format = value?.ToString();
                    if (string.Equals(format, "json", System.StringComparison.OrdinalIgnoreCase)) Format = global::app.test.Format.Json;
                    else if (string.Equals(format, "junit", System.StringComparison.OrdinalIgnoreCase)) Format = global::app.test.Format.JUnit;
                    else return Context.Error(ConfigError($"--test.format must be \"json\" or \"junit\", got {Describe(value)}"));
                    break;
                }
                // Unknown keys are ignored — forward-compatible with future options.
            }
        }
        return Context.Ok();
    }

    private static ServiceError ConfigError(string message) =>
        new(message, "InvalidTestConfig", 400);

    private static bool TryToInt(object? value, out int result)
    {
        switch (value)
        {
            case int i: result = i; return true;
            case long l when l >= int.MinValue && l <= int.MaxValue: result = (int)l; return true;
            case double d when d == System.Math.Truncate(d): result = (int)d; return true;
            case string s when int.TryParse(s, out var parsed): result = parsed; return true;
            default: result = 0; return false;
        }
    }

    private static IEnumerable<string> ToStringList(object? value)
    {
        if (value is string s) return [s];
        if (value is System.Collections.IEnumerable en) return en.Cast<object?>().Select(v => v?.ToString() ?? "").Where(v => v.Length > 0);
        return [];
    }

    private static string Describe(object? value) => value switch
    {
        null => "(null)",
        string s => $"\"{s}\"",
        _ => $"{value} ({value.GetType().Name})"
    };
}
