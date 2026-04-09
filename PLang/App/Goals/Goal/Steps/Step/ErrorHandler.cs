namespace App.Goals.Goal.Steps.Step;

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
    [Store]
    public GoalCall? Goal { get; init; }

    [Store]
    public int? RetryCount { get; init; }

    [Store]
    public int? RetryOverMs { get; init; }

    [Store]
    public ErrorOrder? Order { get; init; }

    [Store]
    public bool IgnoreError { get; init; }

    [Store]
    public string? Message { get; init; }

    [Store]
    public int? StatusCode { get; init; }

    [Store]
    public string? Key { get; init; }
}

