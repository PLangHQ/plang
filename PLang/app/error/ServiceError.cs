using app.actor.context;
using Call = app.callstack.call.@this;

namespace app.error;

/// <summary>
/// Error that occurred inside a service/handler implementation.
/// Example: a handler encounters an internal failure while processing.
/// </summary>
public class ServiceError : Error
{
    public ServiceError(string message, string key = "ServiceError", global::app.type.item.status.@this? status = null)
        : base(message, key, status) { }

    public ServiceError(string message, Step step, string key = "ServiceError", global::app.type.item.status.@this? status = null)
        : base(message, step, key, status) { }

    public ServiceError(string message, Step step, IReadOnlyList<Call> callFrames, string key = "ServiceError", global::app.type.item.status.@this? status = null)
        : base(message, step, callFrames, key, status) { }

    public ServiceError(string message, actor.context.@this context, string key = "ServiceError", global::app.type.item.status.@this? status = null)
        : base(message, context, key, status) { }

    public new static ServiceError FromException(Exception ex, string key = "Exception", global::app.type.item.status.@this? status = null)
    {
        return new ServiceError(ex.Message, key, status ?? 500)
        {
            Exception = ex
        };
    }
}
