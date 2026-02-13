using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.assert;

[Action("isFalse")]
public partial class IsFalse : IContext
{
    public partial object? Value { get; init; }
    public partial string? Message { get; init; }

    public Task<Data> Run()
    {
        if (!AssertHelper.IsTruthy(Value))
            return Task.FromResult(Data.Ok(true));

        return Task.FromResult(Data.FromError(
            new AssertionError(false, Value, Message ?? "Expected falsy value")));
    }
}
