namespace PLang.Runtime2;

/// <summary>
/// Order of error handling: retry first or call goal first.
/// </summary>
public enum ErrorOrder
{
    GoalFirst,
    RetryFirst
}

/// <summary>
/// Represents error handling configuration for a step.
/// </summary>
public sealed class ErrorHandler
{
    /// <summary>
    /// Goal to call when an error occurs.
    /// </summary>
    public GoalCall? Goal { get; init; }

    /// <summary>
    /// Number of times to retry the step.
    /// </summary>
    public int? RetryCount { get; init; }

    /// <summary>
    /// Total time window in seconds over which retries should occur.
    /// </summary>
    public int? RetryOverSeconds { get; init; }

    /// <summary>
    /// Order of error handling operations.
    /// </summary>
    public ErrorOrder? Order { get; init; }

    /// <summary>
    /// Whether to ignore the error and continue execution.
    /// </summary>
    public bool IgnoreError { get; init; }

    /// <summary>
    /// Optional message to display or log when error occurs.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Status code to filter which errors this handler handles.
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// User-defined key for the error handler.
    /// </summary>
    public string? Key { get; init; }
}

