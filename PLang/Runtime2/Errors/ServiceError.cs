using PLang.Runtime2.Context;

namespace PLang.Runtime2.Errors;

/// <summary>
/// Error that occurred inside a service/handler implementation.
/// Example: a handler encounters an internal failure while processing.
/// </summary>
public class ServiceError : Error
{
    public ServiceError(string message, string key = "ServiceError", int statusCode = 400)
        : base(message, key, statusCode) { }

    public ServiceError(string message, PLangContext context, string key = "ServiceError", int statusCode = 400)
        : base(message, context, key, statusCode) { }

    public new static ServiceError FromException(Exception ex, string key = "Exception", int statusCode = 500)
    {
        return new ServiceError(ex.Message, key, statusCode)
        {
            Exception = ex,
            InnerError = ex.InnerException != null ? Error.FromException(ex.InnerException) : null
        };
    }

    public new static ServiceError FromException(Exception ex, PLangContext context, string key = "Exception", int statusCode = 500)
    {
        return new ServiceError(ex.Message, context, key, statusCode)
        {
            Exception = ex,
            InnerError = ex.InnerException != null ? Error.FromException(ex.InnerException) : null
        };
    }
}
