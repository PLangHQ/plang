using Duration = global::app.type.item.duration.@this;
using Item = global::app.type.item.@this;

using PLang.Tests.App.Fixtures;

namespace PLang.Tests.App.ScalarsAsNative;

// duration.@this is backed by TimeSpan. Full wrapper: parts, ordering, equality,
// truthiness (zero is falsy — documented policy), bare ISO-8601 serialize.
public class DurationWrapperTests
{
    [Test]
    public async Task Duration_Order_ByLength()
    {
        await using var app = new global::app.@this("/app").Testing();
        var s = new Duration(System.TimeSpan.FromSeconds(1));
        var m = new Duration(System.TimeSpan.FromMinutes(1));
        var h = new Duration(System.TimeSpan.FromHours(1));
        await Assert.That(CompareTestOps.Ord(app.actor.list.User.Context, s, m)).IsLessThan(0);
        await Assert.That(CompareTestOps.Ord(app.actor.list.User.Context, m, h)).IsLessThan(0);
        await Assert.That(CompareTestOps.OrdD(new Data("", s), new Data("", h))).IsLessThan(0);
    }

    [Test]
    public async Task Duration_Equality_SameTimeSpanValueEqualAndHashEqual()
    {
        var a = new Duration(System.TimeSpan.FromSeconds(90));
        var b = new Duration(System.TimeSpan.FromMinutes(1.5));
        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    // each unit is the whole span in it, a number: 1m30s is 1.5 minutes, 90 seconds, 90000 milliseconds
    public async Task Duration_EachUnit_IsTheWholeSpan()
    {
        var d = new Duration(System.TimeSpan.FromSeconds(90));
        await Assert.That(d.Seconds.ToString()).IsEqualTo("90");
        await Assert.That(d.Minutes.ToString()).IsEqualTo("1.5");
        await Assert.That(d.Milliseconds.ToString()).IsEqualTo("90000");
        await Assert.That(d.Hours.AsDouble()).IsEqualTo(0.025);
        await Assert.That(new Duration(System.TimeSpan.FromHours(36)).Days.ToString()).IsEqualTo("1.5");
    }

    [Test]
    public async Task Duration_Truthiness_ZeroVsNonZeroPolicyDocumented()
    {
        // Documented policy: zero duration is falsy; any non-zero span is truthy.
        Item zero = new Duration(System.TimeSpan.Zero);
        Item nonZero = new Duration(System.TimeSpan.FromSeconds(1));
        await Assert.That(zero.IsTruthy()).IsFalse();
        await Assert.That(nonZero.IsTruthy()).IsTrue();
    }

    [Test]
    public async Task Duration_BareSerialize_InItsKind_RoundTrips()
    {
        // A span made with no text is short, written bare in its largest whole unit, and reads back as itself.
        var d = new Duration(System.TimeSpan.FromMinutes(90));
        var text = d.ToString();
        await Assert.That(text).IsEqualTo("90m");
        await Assert.That(Duration.Resolve(text, null!)!.Value).IsEqualTo(d.Value);
    }

    [Test]
    public async Task Duration_OwnsClrTimeSpan_RegisteredInOwnedClrTypes()
    {
        await Assert.That(Duration.OwnedClrTypes.Any(o => o.Clr == typeof(System.TimeSpan))).IsTrue();
    }
}
