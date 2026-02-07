using PLang.Runtime2.Context;

namespace PLang.Runtime2.Errors;

/// <summary>
/// Error that occurred inside an action execution.
/// Example: file.read finds file does not exist, variable.get with missing name.
/// Captures the action class and method that failed.
/// </summary>
public class ActionError : Error
{
    public string? ActionClass { get; init; }
    public string? ActionMethod { get; init; }

    public ActionError(string message, string key = "ActionError", int statusCode = 400)
        : base(message, key, statusCode) { }

    public ActionError(string message, PLangContext context, string key = "ActionError", int statusCode = 400)
        : base(message, context, key, statusCode) { }

    public new static ActionError FromException(Exception ex, string key = "Exception", int statusCode = 500)
    {
        return new ActionError(ex.Message, key, statusCode)
        {
            Exception = ex,
            InnerError = ex.InnerException != null ? Error.FromException(ex.InnerException) : null
        };
    }

    public new static ActionError FromException(Exception ex, PLangContext context, string key = "Exception", int statusCode = 500)
    {
        return new ActionError(ex.Message, context, key, statusCode)
        {
            Exception = ex,
            InnerError = ex.InnerException != null ? Error.FromException(ex.InnerException) : null
        };
    }

    public static ActionError NotFound(string what) => new($"{what} not found", "ActionNotFound", 404);
    public static ActionError NotFound(string what, PLangContext context) => new($"{what} not found", context, "ActionNotFound", 404);
}
