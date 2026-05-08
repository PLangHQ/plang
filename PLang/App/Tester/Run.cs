using System.Diagnostics;
using App.Errors;

namespace App.Tester;

/// <summary>
/// Execution record for a single test. Created per Ready File by test.run.
/// Lives on the per-test child App's Testing.CurrentTest during execution,
/// then added to the parent's Testing.Results when the test completes.
/// </summary>
public sealed class Run
{
    private readonly Stopwatch _stopwatch;

    /// <summary>The discovered File this run is executing.</summary>
    public File File { get; }

    /// <summary>Current lifecycle status. Transitions on Complete().</summary>
    public Status Status { get; private set; }

    /// <summary>Wall-clock duration from construction to Complete(). Zero until Complete() fires.</summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>Error captured when a test fails or errors. Carries AssertionError.Variables on assertion failures.</summary>
    public IError? Error { get; private set; }

    /// <summary>Output produced via output.write during the test. Rendered on failure when verbose is off.</summary>
    public string? CapturedOutput { get; set; }

    /// <summary>Tags added during the run via test.tag. Distinct from File.Tags (which is discovery-time).</summary>
    public HashSet<string> UserTags { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Run(File file)
    {
        File = file;
        Status = file.Status == Status.Ready ? Status.Ready : file.Status;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>Transitions to the given terminal status and records elapsed duration.</summary>
    public void Complete(Status status, IError? error = null)
    {
        if (_stopwatch.IsRunning) _stopwatch.Stop();
        Duration = _stopwatch.Elapsed;
        Status = status;
        Error = error;
    }

    /// <summary>
    /// Completes based on a Data result: success → Pass; failure → Fail carrying the error.
    /// Skipped/Stale/Timeout have dedicated Complete(status) calls.
    /// </summary>
    public void Complete(Data.@this result)
    {
        if (result.Success) Complete(Status.Pass);
        else Complete(Status.Fail, result.Error);
    }
}
