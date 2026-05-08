namespace App.Tester;

/// <summary>
/// Lifecycle status of a discovered or executed test.
/// </summary>
public enum Status
{
    /// <summary>Discovered, .pr fresh — ready to run.</summary>
    Ready,
    /// <summary>Execution finished, no assertion failed.</summary>
    Pass,
    /// <summary>Execution finished, an assertion or error produced a failing result.</summary>
    Fail,
    /// <summary>Execution was cancelled because it exceeded the configured timeout.</summary>
    Timeout,
    /// <summary>A .pr is missing or doesn't match the current .goal hash. Test not run.</summary>
    Stale,
    /// <summary>Filtered out by include/exclude tag rules. Test not run but reported.</summary>
    Skipped
}
