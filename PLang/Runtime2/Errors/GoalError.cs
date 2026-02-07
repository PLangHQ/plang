using PLang.Runtime2.Context;

namespace PLang.Runtime2.Errors;

/// <summary>
/// Error at the goal level.
/// Example: goal not found, goal execution cancelled.
/// </summary>
public class GoalError : Error
{
    public GoalError(string message, string key = "GoalError", int statusCode = 400)
        : base(message, key, statusCode) { }

    public GoalError(string message, PLangContext context, string key = "GoalError", int statusCode = 400)
        : base(message, context, key, statusCode) { }

    public new static GoalError FromException(Exception ex, string key = "Exception", int statusCode = 500)
    {
        return new GoalError(ex.Message, key, statusCode)
        {
            Exception = ex,
            InnerError = ex.InnerException != null ? Error.FromException(ex.InnerException) : null
        };
    }

    public static GoalError NotFound(string goalName) => new($"Goal '{goalName}' not found", "NotFound", 404) { GoalName = goalName };
    public static GoalError Cancelled() => new("Execution cancelled", "Cancelled", 499);
    public static GoalError Cancelled(PLangContext context) => new("Execution cancelled", context, "Cancelled", 499);
}
