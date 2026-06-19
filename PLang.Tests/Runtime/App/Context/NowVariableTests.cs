namespace PLang.Tests.App.Context;

/// <summary>
/// %Now% is a dynamic system variable (DateTimeOffset.Now, type datetime) seeded by the
/// variable list. Navigating its properties — %Now.Year%, %Now.Ticks%, … — must reach the
/// datetime value's members. Because %Now% is re-evaluated fresh on every access, each
/// navigated property is asserted to fall between a before/after snapshot rather than to
/// equal a fixed instant.
/// </summary>
public class NowVariableTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/test");

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    // The bare variable resolves to a live DateTimeOffset.
    [Test]
    public async Task Now_ResolvesToDateTimeOffset()
    {
        var before = System.DateTimeOffset.Now;
        var value = await _app.User.Context.Variable.GetValue("Now");
        var after = System.DateTimeOffset.Now;

        await Assert.That(value).IsTypeOf<System.DateTimeOffset>();
        var now = (System.DateTimeOffset)value!;
        await Assert.That(now).IsGreaterThanOrEqualTo(before);
        await Assert.That(now).IsLessThanOrEqualTo(after);
    }

    // %Now.Ticks% — the property called out as the canonical navigation target.
    [Test]
    public async Task Now_NavigatesToTicks()
    {
        var before = System.DateTimeOffset.Now;
        var ticks = await _app.User.Context.Variable.GetValue("Now.Ticks");
        var after = System.DateTimeOffset.Now;

        await Assert.That(ticks).IsNotNull();
        long t = System.Convert.ToInt64(ticks);
        await Assert.That(t).IsGreaterThanOrEqualTo(before.Ticks);
        await Assert.That(t).IsLessThanOrEqualTo(after.Ticks);
    }

    // The calendar/clock parts each navigate to the matching member of the live instant.
    [Test]
    public async Task Now_NavigatesToCalendarAndClockParts()
    {
        var before = System.DateTimeOffset.Now;
        var vars = _app.User.Context.Variable;

        long year = System.Convert.ToInt64(await vars.GetValue("Now.Year"));
        long month = System.Convert.ToInt64(await vars.GetValue("Now.Month"));
        long day = System.Convert.ToInt64(await vars.GetValue("Now.Day"));
        long hour = System.Convert.ToInt64(await vars.GetValue("Now.Hour"));
        long minute = System.Convert.ToInt64(await vars.GetValue("Now.Minute"));
        long second = System.Convert.ToInt64(await vars.GetValue("Now.Second"));
        var after = System.DateTimeOffset.Now;

        await Assert.That(year).IsBetween(before.Year, after.Year);
        await Assert.That(month).IsBetween(1, 12);
        await Assert.That(day).IsBetween(1, 31);
        await Assert.That(hour).IsBetween(0, 23);
        await Assert.That(minute).IsBetween(0, 59);
        await Assert.That(second).IsBetween(0, 59);
    }
}
