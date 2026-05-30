using app.error;
using app.variable;

namespace app.module.mock;

[Action("verify", Cacheable = false)]
public partial class Verify : IContext
{
    public partial data.@this<global::app.mock.@this> Mock { get; init; }
    public partial data.@this<int> ExpectedCount { get; init; }
    public partial data.@this<string>? Message { get; init; }

    public Task<data.@this<bool>> Run()
    {
        if (Mock.Value!.CallCount != ExpectedCount.Value)
        {
            return Task.FromResult(global::app.data.@this<bool>.FromError(new AssertionError(
                ExpectedCount.Value, Mock.Value!.CallCount,
                Message?.Value ?? $"Expected {Mock.Value!.Pattern} to be called {ExpectedCount.Value} time(s), but was called {Mock.Value!.CallCount} time(s)")));
        }

        return Task.FromResult(global::app.data.@this<bool>.Ok(true));
    }
}
