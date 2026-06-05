namespace PLang.Tests.App.ScalarsAsNative;

// duration.@this is backed by TimeSpan. Full wrapper: ops, ordering, equality,
// truthiness (zero-vs-non-zero policy documented), bare serializer.
public class DurationWrapperTests
{
    [Test]
    public async Task Duration_Order_ByLength()
    {
        // 1s < 1m < 1h via IOrderableValue routed by Compare.Order.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Duration_Equality_SameTimeSpanValueEqualAndHashEqual()
    {
        // 90s and 1.5m equal AND hash-equal — same TimeSpan, same wrapper, same hash.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Duration_Parts_DaysHoursMinutesSecondsTotals()
    {
        // Parts on the wrapper, not via raw TimeSpan cast at the call site.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Duration_Truthiness_ZeroVsNonZeroPolicyDocumented()
    {
        // Settled by branch: zero-duration is falsy (or truthy — coder picks, comment locks it).
        // The point is a single documented answer through IBooleanResolvable.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Duration_BareSerialize_IsoOrFormattedOnApplicationJson()
    {
        // Normalize emits bare (ISO 8601 duration "PT1H30M" or HH:mm:ss — coder's call,
        // documented). Not enveloped.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Duration_OwnsClrTimeSpan_RegisteredInOwnedClrTypes()
    {
        // primitive map declares duration owns TimeSpan; UnwrapJsonElement /
        // construction-seam materialize TimeSpan-shaped values as duration.@this.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
