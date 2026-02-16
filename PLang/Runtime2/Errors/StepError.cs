using PLang.Runtime2;
using PLang.Runtime2.Context;

namespace PLang.Runtime2.Errors;

/// <summary>
/// Error at the step level.
/// Example: unhandled exception during step execution.
/// </summary>
public class StepError : Error
{
    public override ErrorCategory Category => ErrorCategory.Runtime;
    public StepError(string message, string key = "StepError", int statusCode = 400)
        : base(message, key, statusCode) { }

    public StepError(string message, Step step, string key = "StepError", int statusCode = 400)
        : base(message, step, key, statusCode) { }

    public StepError(string message, PLangContext context, string key = "StepError", int statusCode = 400)
        : base(message, context, key, statusCode) { }

    public new static StepError FromException(Exception ex, PLangContext context, string key = "Exception", int statusCode = 500)
    {
        return new StepError(ex.Message, context, key, statusCode)
        {
            Exception = ex
        };
    }
}
