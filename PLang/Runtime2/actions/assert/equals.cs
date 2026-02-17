using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.assert;

[Action("equals")]
public partial class Equals : IContext
{
    public partial object? Expected { get; init; }
    public partial object? Actual { get; init; }
    public partial string? Message { get; init; }

    public Task<Data> Run()
    {
        if (AssertHelper.AreEqual(Expected, Actual))
            return Task.FromResult(Data.Ok(true));

        return Task.FromResult(Data.FromError(
            new AssertionError(Expected, Actual, Message)));
    }
}
