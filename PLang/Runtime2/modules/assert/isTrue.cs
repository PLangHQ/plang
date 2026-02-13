using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.assert;

[Action("isTrue")]
public partial class IsTrue : IContext
{
    public partial object? Value { get; init; }
    public partial string? Message { get; init; }

    public Task<Data> Run()
    {
        if (AssertHelper.IsTruthy(Value))
            return Task.FromResult(Data.Ok(true));

        return Task.FromResult(Data.FromError(
            new AssertionError(true, Value, Message ?? "Expected truthy value")));
    }
}
