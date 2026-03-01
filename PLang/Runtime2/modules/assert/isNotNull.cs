using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.assert;

[Action("isNotNull")]
public partial class IsNotNull : IContext
{
    public partial object? Value { get; init; }
    public partial string? Message { get; init; }

    public Task<Data> Run()
    {
        if (Value != null)
            return Task.FromResult(Data.Ok(true));

        return Task.FromResult(Data.FromError(
            new AssertionError("(not null)", null, Message ?? "Expected non-null value")));
    }
}
