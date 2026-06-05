namespace PLang.Tests.App.ScalarsAsNative;

// datetime.@this is backed by DateTimeOffset, also accepts CLR DateTime on construction.
// Implements IOrderableValue (offset compare), IEquatableValue, IBooleanResolvable.
// Serializes bare as ISO ("o") on application/json.
public class DateTimeWrapperTests
{
    [Test]
    public async Task DateTime_AcceptsClrDateTime_LandsAsDatetime()
    {
        // primitive.cs already aliases `DateTime → datetime`; the wrapper accepts
        // a CLR DateTime on its ctor (or via construction-seam build) and stores
        // it as DateTimeOffset.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task DateTime_Order_ChronologicalUnderItem()
    {
        // earlier < later via IOrderableValue routed by Compare.Order.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task DateTime_Equality_SameInstantValueEqualAndHashEqual()
    {
        // Two datetime.@this for the same instant — Equal AND hash-equal (HashSet usable).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task DateTime_Truthiness_AlwaysTruthy()
    {
        // A datetime value exists → truthy. (Documented policy; epoch is still truthy.)
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task DateTime_Parts_YearMonthDayHourMinuteSecond()
    {
        // Parts accessors on the wrapper, not via raw DateTimeOffset cast at the call site.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task DateTime_BareSerialize_IsoOnApplicationJson()
    {
        // Normalize emits bare ISO ("o" round-trip), not a {"value":"..."} envelope.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
