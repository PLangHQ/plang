using app.actor.context;

namespace app.error;

/// <summary>
/// Error at the step level.
/// Example: unhandled exception during step execution.
/// </summary>
public class StepError : Error
{
    public StepError(string message, string key = "StepError", global::app.type.item.status.@this? status = null)
        : base(message, key, status) { }

    public StepError(string message, Step step, string key = "StepError", global::app.type.item.status.@this? status = null)
        : base(message, step, key, status) { }

    public StepError(string message, actor.context.@this context, string key = "StepError", global::app.type.item.status.@this? status = null)
        : base(message, context, key, status) { }
}
