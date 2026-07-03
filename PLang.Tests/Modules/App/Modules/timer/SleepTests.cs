using static PLang.Tests.TestAction;

namespace PLang.Tests.App.Modules.timer;

public class SleepTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.User.Context;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/app");
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    [Test]
    public async Task Sleep_CompletesNormally_ReturnsOk()
    {
        var action = Create("timer", "sleep", ("ms", 1));

        var result = await action.RunAsync(Ctx);

        await result.IsSuccess();
    }
}
