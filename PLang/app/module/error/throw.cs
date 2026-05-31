using app.error;
using app.variable;

namespace app.module.error;

[Action("throw", Cacheable = false)]
public partial class Throw : IContext
{
    // Untyped: the message is usually a string, but `- throw %!error%` re-raises
    // an existing Error object — it must arrive intact, not stringified into a
    // string slot (binding would fail and mask the original error).
    [IsNotNull]
    public partial data.@this Message { get; init; }
    [Default(500)]
    public partial data.@this<int> StatusCode { get; init; }
    public partial data.@this<string>? Key { get; init; }

    public Task<data.@this> Run()
    {
        // Re-throw an existing error as-is — `- throw %!error%` preserves Key,
        // Message, StatusCode, and the error chain rather than stringifying it.
        // A first-class, intended pattern.
        if (Message.Value is global::app.error.IError existing)
            return Task.FromResult(Error(existing));

        return Task.FromResult(Error(
            new ServiceError(Message.Value?.ToString() ?? "", Key?.Value ?? "UserError", StatusCode.Value)));
    }
}
