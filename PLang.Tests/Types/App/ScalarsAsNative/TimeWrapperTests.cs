using TimeT = global::app.type.item.time.@this;
using DateTimeT = global::app.type.item.datetime.@this;

using PLang.Tests.App.Fixtures;

namespace PLang.Tests.App.ScalarsAsNative;

// time.@this is its own type, backed by TimeOnly. Today ScalarComparer has no
// TimeOnly arm at all — this wrapper closes that gap. OwnedClrTypes = TimeOnly.
public class TimeWrapperTests : IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
    // a time made by the app's type for it
    private TimeT Time(int hour, int minute, int second = 0) => new(new System.TimeOnly(hour, minute, second), _app.type.list[typeof(TimeT)]);

    private static global::app.@this NewApp()
        => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-time-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor);

    [Test]
    public async Task Time_IsDistinctFromDatetime_TypeNameIsTime()
    {
        await using var app = NewApp();
        var d = new Data("", Time(10, 30), context: app.actor.list.User.Context);
        await Assert.That(d.Type.Name).IsEqualTo("time");
        await Assert.That(typeof(DateTimeT).IsAssignableFrom(typeof(TimeT))).IsFalse();
        await Assert.That(TimeT.OwnedClrTypes.Any(o => o.Clr == typeof(System.TimeOnly))).IsTrue();
    }

    [Test]
    public async Task Time_Order_WithinTimeOnly()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var morning = Time(9, 0);
        var afternoon = Time(14, 30);
        var night = Time(23, 59);
        await Assert.That(CompareTestOps.Ord(app.actor.list.User.Context, morning, afternoon)).IsLessThan(0);
        await Assert.That(CompareTestOps.Ord(app.actor.list.User.Context, afternoon, night)).IsLessThan(0);
        await Assert.That(CompareTestOps.Ord(app.actor.list.User.Context, morning, Time(9, 0))).IsEqualTo(0);
    }

    [Test]
    public async Task Time_Equality_SameTimeValueEqualAndHashEqual()
    {
        var a = Time(14, 30, 0);
        var b = Time(14, 30, 0);
        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task Time_BareSerialize_IsoTimeOnApplicationJson()
    {
        // Bare ISO time form, not promoted to a datetime.
        await Assert.That(Time(10, 30, 0).ToString()).StartsWith("10:30:00");
    }
}
