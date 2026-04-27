using App.Errors;
using App.Variables;

namespace App.modules.mock;

[System.ComponentModel.Description("Assert that a mock was called exactly ExpectedCount times; fails the step if the count differs")]
[Action("verify", Cacheable = false)]
public partial class Verify : IContext
{
    public partial Data.@this<types.MockHandle> Mock { get; init; }
    public partial Data.@this<int> ExpectedCount { get; init; }
    public partial Data.@this<string>? Message { get; init; }

    public Task<Data.@this> Run()
    {
        if (Mock.Value!.CallCount != ExpectedCount.Value)
        {
            return Task.FromResult(Error(new AssertionError(
                ExpectedCount.Value, Mock.Value!.CallCount,
                Message?.Value ?? $"Expected {Mock.Value!.ActionPattern} to be called {ExpectedCount.Value} time(s), but was called {Mock.Value!.CallCount} time(s)")));
        }

        return Task.FromResult(Data(true));
    }
}
