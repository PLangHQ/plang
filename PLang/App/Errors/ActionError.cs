using System.Text;
using App.Actor.Context;

namespace App.Errors;

/// <summary>
/// Error that occurred inside an action execution.
/// Example: file.read finds file does not exist, variable.get with missing name.
/// Captures the action class and method that failed.
/// </summary>
public class ActionError : Error
{
    public string? ActionModule { get; init; }
    public string? ActionName { get; init; }

    public ActionError(string message, string key = "ActionError", int statusCode = 400)
        : base(message, key, statusCode) { }

    public ActionError(string message, Step step, string key = "ActionError", int statusCode = 400)
        : base(message, step, key, statusCode) { }

    public ActionError(string message, Actor.Context.@this context, string key = "ActionError", int statusCode = 400)
        : base(message, context, key, statusCode) { }

    public new static ActionError FromException(Exception ex, string key = "Exception", int statusCode = 500)
    {
        return new ActionError(ex.Message, key, statusCode)
        {
            Exception = ex
        };
    }

    public new static ActionError FromException(Exception ex, Actor.Context.@this context, string key = "Exception", int statusCode = 500)
    {
        return new ActionError(ex.Message, context, key, statusCode)
        {
            Exception = ex
        };
    }

    public static ActionError NotFound(string what) => new($"{what} not found", "ActionNotFound", 404);
    public static ActionError NotFound(string what, Actor.Context.@this context) => new($"{what} not found", context, "ActionNotFound", 404);

    protected override void FormatExtra(StringBuilder sb, string indent)
    {
        if (ActionModule != null || ActionName != null)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}\ud83d\udce6 Error Source:");
            sb.AppendLine($"{indent}    - The error occurred in the module: `{ActionModule}.{ActionName}`");
        }
    }
}
