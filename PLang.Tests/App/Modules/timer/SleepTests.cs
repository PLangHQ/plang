using static PLang.Tests.TestAction;

namespace PLang.Tests.App.Modules.timer;

public class SleepTests
{
    private global::App.@this _app = null!;
    private global::App.Actor.Context.@this Ctx => _app.User.Context;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::App.@this("/app");
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    [Test]
    public async Task Sleep_CompletesNormally_ReturnsOk()
    {
        var action = Create("timer", "sleep", ("ms", 1));

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
    }
}
