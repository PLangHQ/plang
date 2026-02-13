using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.assert;

[Action("notEquals")]
public partial class NotEquals : IContext
{
    public partial object? Expected { get; init; }
    public partial object? Actual { get; init; }
    public partial string? Message { get; init; }

    public Task<Data> Run()
    {
        if (!AssertHelper.AreEqual(Expected, Actual))
            return Task.FromResult(Data.Ok(true));

        return Task.FromResult(Data.FromError(
            new AssertionError(Expected, Actual,
                Message ?? "Values should not be equal")));
    }
}
