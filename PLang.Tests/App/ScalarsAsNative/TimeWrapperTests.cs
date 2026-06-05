namespace PLang.Tests.App.ScalarsAsNative;

// time.@this is its own type, backed by TimeOnly. Today ScalarComparer has no
// TimeOnly arm at all — this wrapper closes that gap. OwnedClrTypes = TimeOnly.
public class TimeWrapperTests
{
    [Test]
    public async Task Time_IsDistinctFromDatetime_TypeNameIsTime()
    {
        // A Data carrying time.@this reports its type name "time"; not unhandled,
        // not folded into datetime.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Time_Order_WithinTimeOnly()
    {
        // 09:00 < 14:30 < 23:59, same time equals.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Time_Equality_SameTimeValueEqualAndHashEqual()
    {
        // HashSet/dedup behavior — value-equality + hash agree.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Time_BareSerialize_IsoTimeOnApplicationJson()
    {
        // Normalize emits bare ISO time (HH:mm:ss[.fff]), not promoted to a datetime.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
