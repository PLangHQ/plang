
namespace PLang.Tests.App.Modules.timer;

public class SleepTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/app").Testing();
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    [Test]
    public async Task Sleep_CompletesNormally_ReturnsOk()
    {
        var action = Make.Action(Ctx, "timer", "sleep", ("ms", 1));

        var result = await action.Start(Ctx);

        await result.IsSuccess();
    }
}
