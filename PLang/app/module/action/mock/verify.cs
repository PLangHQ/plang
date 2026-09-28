using app.error;

namespace app.module.action.mock;

[Action("verify", Cacheable = false)]
public partial class Verify : IContext
{
    public partial data.@this<global::app.@event.binding.mock.@this> Mock { get; init; }
    public partial data.@this<global::app.type.item.number.@this> ExpectedCount { get; init; }
    public partial data.@this<global::app.type.item.text.@this>? Message { get; init; }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Start()
    {
        var mock = (await Mock.Value())!;
        var expected = await ExpectedCount.Value();
        if (mock.CallCount != expected)
        {
            return Context.Error<global::app.type.item.@bool.@this>(new AssertionError(
                expected!, mock.CallCount,
                (Message == null ? null : (await Message.Value())?.Clr<string>()) ?? $"Expected {mock.Pattern} to be called {expected} time(s), but was called {mock.CallCount} time(s)"));
        }

        return Context.Ok<global::app.type.item.@bool.@this>(true);
    }
}
