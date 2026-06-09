using DateTimeT = global::app.type.datetime.@this;
using Item = global::app.type.item.@this;

using PLang.Tests.App.Fixtures;

namespace PLang.Tests.App.ScalarsAsNative;

// datetime.@this is backed by DateTimeOffset, also accepts CLR DateTime on construction.
// Implements IOrderableValue (instant compare), IEquatableValue, item truthiness.
// Bare wire form is ISO ("o").
public class DateTimeWrapperTests
{
    [Test]
    public async Task DateTime_AcceptsClrDateTime_LandsAsDatetime()
    {
        var dt = new DateTimeT(new System.DateTime(2024, 3, 15, 10, 30, 0, System.DateTimeKind.Utc));
        await Assert.That(dt.Year).IsEqualTo(2024);
        await Assert.That(dt.Value).IsTypeOf<System.DateTimeOffset>();
    }

    [Test]
    public async Task DateTime_Order_ChronologicalUnderItem()
    {
        var earlier = new DateTimeT(System.DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var later = new DateTimeT(System.DateTimeOffset.Parse("2024-06-01T00:00:00Z"));
        await Assert.That(CompareTestOps.OrdD(new Data("", earlier), new Data("", later))).IsLessThan(0);
        await Assert.That(CompareTestOps.Ord(earlier, later)).IsLessThan(0);
    }

    [Test]
    public async Task DateTime_Equality_SameInstantValueEqualAndHashEqual()
    {
        // Same instant, different offset → equal by instant + hash-equal.
        var a = new DateTimeT(System.DateTimeOffset.Parse("2024-01-01T12:00:00+00:00"));
        var b = new DateTimeT(System.DateTimeOffset.Parse("2024-01-01T13:00:00+01:00"));
        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task DateTime_Truthiness_AlwaysTruthy()
    {
        Item epoch = new DateTimeT(System.DateTimeOffset.UnixEpoch);
        await Assert.That(epoch.IsTruthy()).IsTrue();
    }

    [Test]
    public async Task DateTime_Parts_YearMonthDayHourMinuteSecond()
    {
        var dt = new DateTimeT(System.DateTimeOffset.Parse("2024-03-15T10:30:45Z"));
        await Assert.That(dt.Year).IsEqualTo(2024);
        await Assert.That(dt.Month).IsEqualTo(3);
        await Assert.That(dt.Day).IsEqualTo(15);
        await Assert.That(dt.Hour).IsEqualTo(10);
        await Assert.That(dt.Minute).IsEqualTo(30);
        await Assert.That(dt.Second).IsEqualTo(45);
    }

    [Test]
    public async Task DateTime_BareSerialize_IsoOnApplicationJson()
    {
        // The serializer renders the wrapper's bare ISO ("o") form — no envelope.
        var dt = new DateTimeT(System.DateTimeOffset.Parse("2024-03-15T10:30:00Z"));
        var iso = dt.ToString();
        await Assert.That(iso).IsEqualTo(dt.Value.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
        await Assert.That(System.DateTimeOffset.Parse(iso)).IsEqualTo(dt.Value); // round-trips
    }
}
