using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.assert;

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
