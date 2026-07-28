using app.actor.context;

namespace app.error;

/// <summary>
/// Error that occurred inside an action execution.
/// Example: file.read finds file does not exist, variable.get with missing name.
/// </summary>
public class ActionError : Error
{
    public ActionError(string message, string key = "ActionError", int statusCode = 400)
        : base(message, key, statusCode) { }

    public ActionError(string message, Step step, string key = "ActionError", int statusCode = 400)
        : base(message, step, key, statusCode) { }

    public ActionError(string message, actor.context.@this context, string key = "ActionError", int statusCode = 400)
        : base(message, context, key, statusCode) { }

    public new static ActionError FromException(Exception ex, string key = "Exception", int statusCode = 500)
    {
        return new ActionError(ex.Message, key, statusCode)
        {
            Exception = ex
        };
    }

    public new static ActionError FromException(Exception ex, actor.context.@this context, string key = "Exception", int statusCode = 500)
    {
        return new ActionError(ex.Message, context, key, statusCode)
        {
            Exception = ex
        };
    }

    public static ActionError NotFound(string what) => new($"{what} not found", "ActionNotFound", 404);
    public static ActionError NotFound(string what, actor.context.@this context) => new($"{what} not found", context, "ActionNotFound", 404);
}
