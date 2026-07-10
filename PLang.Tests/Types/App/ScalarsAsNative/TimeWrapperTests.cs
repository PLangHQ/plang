using TimeT = global::app.type.item.time.@this;
using DateTimeT = global::app.type.item.datetime.@this;

using PLang.Tests.App.Fixtures;

namespace PLang.Tests.App.ScalarsAsNative;

// time.@this is its own type, backed by TimeOnly. Today ScalarComparer has no
// TimeOnly arm at all — this wrapper closes that gap. OwnedClrTypes = TimeOnly.
public class TimeWrapperTests
{
    private static global::app.@this NewApp()
        => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-time-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test]
    public async Task Time_IsDistinctFromDatetime_TypeNameIsTime()
    {
        await using var app = NewApp();
        var d = new Data("", new TimeT(new System.TimeOnly(10, 30))) { Context = app.User.Context };
        await Assert.That(d.Type.Name).IsEqualTo("time");
        await Assert.That(typeof(DateTimeT).IsAssignableFrom(typeof(TimeT))).IsFalse();
        await Assert.That(TimeT.OwnedClrTypes.Any(o => o.Clr == typeof(System.TimeOnly))).IsTrue();
    }

    [Test]
    public async Task Time_Order_WithinTimeOnly()
    {
        var morning = new TimeT(new System.TimeOnly(9, 0));
        var afternoon = new TimeT(new System.TimeOnly(14, 30));
        var night = new TimeT(new System.TimeOnly(23, 59));
        await Assert.That(CompareTestOps.Ord(morning, afternoon)).IsLessThan(0);
        await Assert.That(CompareTestOps.Ord(afternoon, night)).IsLessThan(0);
        await Assert.That(CompareTestOps.Ord(morning, new TimeT(new System.TimeOnly(9, 0)))).IsEqualTo(0);
    }

    [Test]
    public async Task Time_Equality_SameTimeValueEqualAndHashEqual()
    {
        var a = new TimeT(new System.TimeOnly(14, 30, 0));
        var b = new TimeT(new System.TimeOnly(14, 30, 0));
        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task Time_BareSerialize_IsoTimeOnApplicationJson()
    {
        // Bare ISO time form, not promoted to a datetime.
        await Assert.That(new TimeT(new System.TimeOnly(10, 30, 0)).ToString()).StartsWith("10:30:00");
    }
}
