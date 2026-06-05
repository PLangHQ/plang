namespace PLang.Tests.App.ScalarsAsNative;

// date.@this is its own type, backed by DateOnly. Distinct from datetime — the
// collapse via ScalarComparer's DateOnly → DateTimeOffset coercion ends with
// this branch. OwnedClrTypes = DateOnly.
public class DateWrapperTests
{
    [Test]
    public async Task Date_IsDistinctFromDatetime_TypeNameIsDate()
    {
        // A Data carrying date.@this reports its type name "date", not "datetime".
        // The load-bearing C# pin matching the integration proof in v1.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Date_Order_ChronologicalWithinDateOnly()
    {
        // Day-precision compare; same day equals, day before/after orders.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Date_Equality_SameDayValueEqualAndHashEqual()
    {
        // HashSet/dedup behavior — value-equality + hash agree.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Date_Parts_YearMonthDay()
    {
        // Parts on the wrapper, not via raw DateOnly at the call site.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Date_BareSerialize_IsoYyyyMmDdOnApplicationJson()
    {
        // Normalize emits bare ISO date (yyyy-MM-dd), not promoted to a datetime.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
