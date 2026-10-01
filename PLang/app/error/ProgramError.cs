using app.actor.context;

namespace app.error;

/// <summary>
/// Legacy error type kept for backward compatibility.
/// </summary>
public class ProgramError : Error
{
    public ProgramError(string message, string key = "ProgramError", global::app.type.item.status.@this? status = null)
        : base(message, key, status) { }

    public ProgramError(string message, Step step, string key = "ProgramError", global::app.type.item.status.@this? status = null)
        : base(message, step, key, status) { }

    public ProgramError(string message, actor.context.@this context, string key = "ProgramError", global::app.type.item.status.@this? status = null)
        : base(message, context, key, status) { }

    public new static ProgramError FromException(Exception ex, string key = "Exception", global::app.type.item.status.@this? status = null)
    {
        return new ProgramError(ex.Message, key, status ?? 500)
        {
            Exception = ex
        };
    }
}
