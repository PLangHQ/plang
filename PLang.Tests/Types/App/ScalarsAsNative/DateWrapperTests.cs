using DateT = global::app.type.date.@this;
using DateTimeT = global::app.type.datetime.@this;

using PLang.Tests.App.Fixtures;

namespace PLang.Tests.App.ScalarsAsNative;

// date.@this is its own type, backed by DateOnly. Distinct from datetime — the
// collapse via ScalarComparer's DateOnly → DateTimeOffset coercion ends with
// this branch. OwnedClrTypes = DateOnly.
public class DateWrapperTests
{
    private static global::app.@this NewApp()
        => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-date-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test]
    public async Task Date_IsDistinctFromDatetime_TypeNameIsDate()
    {
        // A Data carrying date.@this reports type "date", not "datetime"; and
        // date.@this is not a datetime.@this. OwnedClrTypes pins DateOnly.
        await using var app = NewApp();
        var d = new Data("", new DateT(new System.DateOnly(2024, 3, 15))) { Context = app.User.Context };
        await Assert.That(d.Type.Name).IsEqualTo("date");
        await Assert.That(typeof(DateTimeT).IsAssignableFrom(typeof(DateT))).IsFalse();
        var owned = DateT.OwnedClrTypes;
        await Assert.That(owned.Any(o => o.Clr == typeof(System.DateOnly))).IsTrue();
    }

    [Test]
    public async Task Date_Order_ChronologicalWithinDateOnly()
    {
        var d1 = new DateT(new System.DateOnly(2024, 1, 1));
        var d2 = new DateT(new System.DateOnly(2024, 1, 2));
        await Assert.That(CompareTestOps.Ord(d1, d2)).IsLessThan(0);
        await Assert.That(CompareTestOps.Ord(d2, d1)).IsGreaterThan(0);
        await Assert.That(CompareTestOps.Ord(d1, new DateT(new System.DateOnly(2024, 1, 1)))).IsEqualTo(0);
    }

    [Test]
    public async Task Date_Equality_SameDayValueEqualAndHashEqual()
    {
        var a = new DateT(new System.DateOnly(2024, 3, 15));
        var b = new DateT(new System.DateOnly(2024, 3, 15));
        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task Date_Parts_YearMonthDay()
    {
        var d = new DateT(new System.DateOnly(2024, 3, 15));
        await Assert.That(d.Year).IsEqualTo(2024);
        await Assert.That(d.Month).IsEqualTo(3);
        await Assert.That(d.Day).IsEqualTo(15);
    }

    [Test]
    public async Task Date_BareSerialize_IsoYyyyMmDdOnApplicationJson()
    {
        // Bare ISO date (yyyy-MM-dd), not promoted to a datetime instant.
        await Assert.That(new DateT(new System.DateOnly(2024, 3, 15)).ToString()).IsEqualTo("2024-03-15");
    }
}
