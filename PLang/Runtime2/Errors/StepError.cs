using PLang.Runtime2.Context;
using PLang.Runtime2.Core;

namespace PLang.Runtime2.Errors;

/// <summary>
/// Error at the step level.
/// Example: unhandled exception during step execution.
/// </summary>
public class StepError : Error
{
    public Step? Step { get; init; }

    public StepError(string message, string key = "StepError", int statusCode = 400)
        : base(message, key, statusCode) { }

    public StepError(string message, PLangContext context, string key = "StepError", int statusCode = 400)
        : base(message, context, key, statusCode) { }

    public new static StepError FromException(Exception ex, PLangContext context, string key = "Exception", int statusCode = 500)
    {
        return new StepError(ex.Message, context, key, statusCode)
        {
            Exception = ex,
            InnerError = ex.InnerException != null ? Error.FromException(ex.InnerException) : null,
            Step = context.Step
        };
    }
}
