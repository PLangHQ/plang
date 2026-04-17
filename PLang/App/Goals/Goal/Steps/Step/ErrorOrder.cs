namespace App.Goals.Goal.Steps.Step;

/// <summary>
/// Order of error handling for the error.handle modifier:
/// RetryFirst — retry the action, then call error goal if retries fail (default)
/// GoalFirst — call the error goal first (e.g. fix preconditions), then retry
/// </summary>
public enum ErrorOrder
{
    GoalFirst,
    RetryFirst
}
