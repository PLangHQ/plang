using app.actor.context;

namespace app.error;

/// <summary>
/// Error that occurred inside an action execution.
/// Example: file.read finds file does not exist, variable.get with missing name.
/// </summary>
public class ActionError : Error
{
    public ActionError(string message, string key = "ActionError", global::app.type.item.status.@this? status = null)
        : base(message, key, status) { }

    public ActionError(string message, Step step, string key = "ActionError", global::app.type.item.status.@this? status = null)
        : base(message, step, key, status) { }

    public ActionError(string message, actor.context.@this context, string key = "ActionError", global::app.type.item.status.@this? status = null)
        : base(message, context, key, status) { }

    public new static ActionError FromException(Exception ex, string key = "Exception", global::app.type.item.status.@this? status = null)
    {
        return new ActionError(ex.Message, key, status ?? 500)
        {
            Exception = ex
        };
    }

    public static ActionError NotFound(string what) => new($"{what} not found", "ActionNotFound", 404);
    public static ActionError NotFound(string what, actor.context.@this context) => new($"{what} not found", context, "ActionNotFound", 404);
}
