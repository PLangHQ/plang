using app.errors;
using app.variables;

namespace app.modules.mock;

[System.ComponentModel.Description("Assert that a mock was called exactly ExpectedCount times; fails the step if the count differs")]
[Action("verify", Cacheable = false)]
public partial class Verify : IContext
{
    public partial data.@this<types.MockHandle> Mock { get; init; }
    public partial data.@this<int> ExpectedCount { get; init; }
    public partial data.@this<string>? Message { get; init; }

    public Task<data.@this> Run()
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
