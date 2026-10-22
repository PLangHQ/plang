namespace PLang.Tests.App.Modules.timer;

// timer.sleep takes a duration: the step's own words (2 seconds, half a second, 500 ms) written as a number and its
// unit, and the sleep lasts that long.
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
        var action = Make.Action(Ctx, "timer", "sleep", ("Duration", "1ms"));

        var result = await action.Start(Ctx);

        await result.IsSuccess();
    }

    // sleep 2 seconds → 2s; half a second → 500ms (or 0.5s); 500 ms → 500ms — each sleeps at least that long (2 read as
    // 2 ms is caught); how much longer is the machine's load, so the upper bound is only a hang guard (a unit read far
    // too long, 2s as 2000s) and checks nothing else
    [Test]
    [Arguments("2s", 2000)]
    [Arguments("0.5s", 500)]
    [Arguments("500ms", 500)]
    public async Task Sleep_LastsItsDuration(string duration, int ms)
    {
        var action = Make.Action(Ctx, "timer", "sleep", ("Duration", duration));
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var result = await action.Start(Ctx);

        await result.IsSuccess();
        await Assert.That(clock.ElapsedMilliseconds).IsGreaterThanOrEqualTo(ms - 15);
        await Assert.That(clock.ElapsedMilliseconds).IsLessThan(60_000);
    }

    // a number alone names no unit: refused, never read as milliseconds
    [Test]
    public async Task Sleep_ANumberWithoutAUnit_IsRefused()
    {
        var action = Make.Action(Ctx, "timer", "sleep", ("Duration", "2000"));

        var result = await action.Start(Ctx);

        await result.IsFailure();
    }
}
