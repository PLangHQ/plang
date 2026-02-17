using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.mock;

[Action("verify", Cacheable = false)]
public partial class Verify : IContext
{
    public partial types.MockHandle Mock { get; init; }
    public partial int ExpectedCount { get; init; }
    public partial string? Message { get; init; }

    public Task<Data> Run()
    {
        if (Mock.CallCount != ExpectedCount)
        {
            return Task.FromResult(Data.FromError(new AssertionError(
                ExpectedCount, Mock.CallCount,
                Message ?? $"Expected {Mock.ActionPattern} to be called {ExpectedCount} time(s), but was called {Mock.CallCount} time(s)")));
        }

        return Task.FromResult(Data.Ok(true));
    }
}
