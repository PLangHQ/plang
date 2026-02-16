using PLang.Runtime2;
using PLang.Runtime2.Context;

namespace PLang.Runtime2.Errors;

/// <summary>
/// Error that occurred inside a service/handler implementation.
/// Example: a handler encounters an internal failure while processing.
/// </summary>
public class ServiceError : Error
{
    public override ErrorCategory Category => ErrorCategory.Runtime;
    public ServiceError(string message, string key = "ServiceError", int statusCode = 400)
        : base(message, key, statusCode) { }

    public ServiceError(string message, Step step, string key = "ServiceError", int statusCode = 400)
        : base(message, step, key, statusCode) { }

    public ServiceError(string message, Step step, IReadOnlyList<CallFrame> callFrames, string key = "ServiceError", int statusCode = 400)
        : base(message, step, callFrames, key, statusCode) { }

    public ServiceError(string message, PLangContext context, string key = "ServiceError", int statusCode = 400)
        : base(message, context, key, statusCode) { }

    public new static ServiceError FromException(Exception ex, string key = "Exception", int statusCode = 500)
    {
        return new ServiceError(ex.Message, key, statusCode)
        {
            Exception = ex
        };
    }

    public new static ServiceError FromException(Exception ex, PLangContext context, string key = "Exception", int statusCode = 500)
    {
        return new ServiceError(ex.Message, context, key, statusCode)
        {
            Exception = ex
        };
    }
}
