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
    public partial data.@this<global::app.type.number.@this> StatusCode { get; init; }
    public partial data.@this<global::app.type.text.@this>? Key { get; init; }

    public async Task<data.@this> Run()
    {
        // Re-throw an existing error as-is — `- throw %!error%` preserves Key,
        // Message, StatusCode, and the error chain rather than stringifying it.
        // A first-class, intended pattern.
        var msg = await Message.Value();
        if (msg is global::app.error.IError existing)
            return Error(existing);

        return Error(
            new ServiceError(msg?.ToString() ?? "", (Key == null ? null : await Key.Value())?.ToString() ?? "UserError", StatusCode.GetValue<int>()));
    }
}
