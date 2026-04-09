using App.Errors;
using App.Variables;

namespace App.modules.mock;

[Action("verify", Cacheable = false)]
public partial class Verify : IContext
{
    public partial types.MockHandle Mock { get; init; }
    public partial int ExpectedCount { get; init; }
    public partial string? Message { get; init; }

    public Task<Data.@this> Run()
    {
        if (Mock.CallCount != ExpectedCount)
        {
            return Task.FromResult(Error(new AssertionError(
                ExpectedCount, Mock.CallCount,
                Message ?? $"Expected {Mock.ActionPattern} to be called {ExpectedCount} time(s), but was called {Mock.CallCount} time(s)")));
        }

        return Task.FromResult(Data(true));
    }
}
