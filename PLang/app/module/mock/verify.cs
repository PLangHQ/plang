using app.error;
using app.variable;

namespace app.module.mock;

[Action("verify", Cacheable = false)]
public partial class Verify : IContext
{
    public partial data.@this<global::app.mock.@this> Mock { get; init; }
    public partial data.@this<global::app.type.number.@this> ExpectedCount { get; init; }
    public partial data.@this<global::app.type.text.@this>? Message { get; init; }

    public async Task<data.@this<global::app.type.@bool.@this>> Run()
    {
        var mock = ((await Mock.Value()) as global::app.mock.@this)!;
        var expected = (await ExpectedCount.Value()) as global::app.type.number.@this;
        if (mock.CallCount != expected)
        {
            return global::app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(
                expected!, mock.CallCount,
                ((Message == null ? null : await Message.Value()) as global::app.type.text.@this) ?? $"Expected {mock.Pattern} to be called {expected} time(s), but was called {mock.CallCount} time(s)"));
        }

        return global::app.data.@this<global::app.type.@bool.@this>.Ok(true);
    }
}
