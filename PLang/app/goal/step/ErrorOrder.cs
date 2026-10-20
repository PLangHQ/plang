namespace app.goal.step;

/// <summary>
/// Order of error handling for the on.error modifier:
/// RetryFirst — retry the action, then call error goal if retries fail (default)
/// GoalFirst — call the error goal first (e.g. fix preconditions), then retry
/// </summary>
[global::app.Attributes.PlangType("errororder"), global::app.Attributes.Choice]
public enum ErrorOrder
{
    GoalFirst,
    RetryFirst
}
