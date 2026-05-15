using app.actor.context;

namespace app.errors;

/// <summary>
/// Error at the goal level.
/// Example: goal not found, goal execution cancelled.
/// </summary>
public class GoalError : Error
{
    public override ErrorCategory Category => ErrorCategory.Runtime;
    public GoalError(string message, string key = "GoalError", int statusCode = 400)
        : base(message, key, statusCode) { }

    public GoalError(string message, Step step, string key = "GoalError", int statusCode = 400)
        : base(message, step, key, statusCode) { }

    public GoalError(string message, actor.context.@this context, string key = "GoalError", int statusCode = 400)
        : base(message, context, key, statusCode) { }

    public new static GoalError FromException(Exception ex, string key = "Exception", int statusCode = 500)
    {
        return new GoalError(ex.Message, key, statusCode)
        {
            Exception = ex
        };
    }

    public static GoalError NotFound(string goalName) => new($"Goal '{goalName}' not found", "NotFound", 404);
    public static GoalError Cancelled() => new("Execution cancelled", "Cancelled", 499);
    public static GoalError Cancelled(actor.context.@this context) => new("Execution cancelled", context, "Cancelled", 499);
}
