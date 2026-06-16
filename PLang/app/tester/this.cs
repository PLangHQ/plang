using app.error;

namespace app.tester;

/// <summary>
/// Test runner state and config. Owns the per-run Results collection, the per-run
/// Coverage tracker, and the per-test in-flight Run slot. Configured via --test={...}.
/// When IsEnabled is on, downstream systems can observe it (in-memory DBs, stub identity, etc.)
/// and test.tag / assert handlers use the state on this object.
/// Activated by: plang --test
/// </summary>
[global::app.Attributes.PlangType("test")]
public sealed partial class @this
{
    /// <summary>Whether test mode is active. Set by --test, read by subsystems that branch on test mode.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Run-wide collection of TestRuns. Each test's App contributes one Run at completion.</summary>
    public Results Results { get; } = new();

    /// <summary>Run-wide coverage tracker. Per-test Apps populate their own, then Merge into the parent here.</summary>
    public Coverage Coverage { get; } = new();

    /// <summary>The test currently in flight on this App. null when no test is running. test.run assigns; test.tag reads.</summary>
    public Run? CurrentTest { get; set; }

    // --- Configuration fields (flat per architect spec — no nested Config class) ---

    /// <summary>Per-test wall-clock timeout in seconds. Default 30.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Parallelism bound for test.run's semaphore. Default Environment.ProcessorCount.</summary>
    public int Parallel { get; set; } = Environment.ProcessorCount;

    /// <summary>Include tag filter (empty = all tests match). Case-insensitive.</summary>
    public HashSet<string> Include { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Exclude tag filter (empty = nothing excluded). Applied after include — exclude wins on conflict.</summary>
    public HashSet<string> Exclude { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>When true, per-test output.write streams live to stdout. When false, captured and rendered only on failure.</summary>
    public bool Verbose { get; set; }

    /// <summary>File report format: "json" (default) or "junit". Console is always written regardless.</summary>
    public string Format { get; set; } = "json";

    /// <summary>Back-reference to the App that owns this Testing. Used by reporters to surface App.Version for drift comparisons.</summary>
    internal app.@this App { get; }

    public @this(app.@this app) { App = app; }

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
                        return data.@this.FromError(ConfigError($"--test.timeout must be an integer, got {Describe(value)}"));
                    if (timeout <= 0)
                        return data.@this.FromError(ConfigError($"--test.timeout must be positive, got {timeout}"));
                    TimeoutSeconds = timeout;
                    break;
                }
                case "parallel":
                {
                    if (!TryToInt(value, out var parallel))
                        return data.@this.FromError(ConfigError($"--test.parallel must be an integer, got {Describe(value)}"));
                    if (parallel <= 0)
                        return data.@this.FromError(ConfigError($"--test.parallel must be positive, got {parallel}"));
                    Parallel = parallel;
                    break;
                }
                case "include":
                {
                    Include.Clear();
                    foreach (var tag in ToStringList(value))
                        Include.Add(tag);
                    break;
                }
                case "exclude":
                {
                    Exclude.Clear();
                    foreach (var tag in ToStringList(value))
                        Exclude.Add(tag);
                    break;
                }
                case "verbose":
                {
                    if (value is bool b) Verbose = b;
                    else if (value is string s && bool.TryParse(s, out var bs)) Verbose = bs;
                    else return data.@this.FromError(ConfigError($"--test.verbose must be a boolean, got {Describe(value)}"));
                    break;
                }
                case "format":
                {
                    var format = value?.ToString();
                    if (format != "json" && format != "junit")
                        return data.@this.FromError(ConfigError($"--test.format must be \"json\" or \"junit\", got {Describe(value)}"));
                    Format = format;
                    break;
                }
                // Unknown keys are ignored — forward-compatible with future options.
            }
        }
        return data.@this.Ok();
    }

    private static ServiceError ConfigError(string message) =>
        new(message, "InvalidTestConfig", 400);

    private static bool TryToInt(object? value, out int result)
    {
        switch (value)
        {
            case int i: result = i; return true;
            case long l when l >= int.MinValue && l <= int.MaxValue: result = (int)l; return true;
            case double d when d == Math.Truncate(d): result = (int)d; return true;
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
