using Number = global::app.type.number.@this;

using PLang.Tests.App.Fixtures;

namespace PLang.Tests.App.ScalarsAsNative;

// number.@this is the reference-shape wrapper (already complete pre-branch).
// After it becomes `: item.@this`, its behavior must be unchanged — these
// tests pin "no regression on the worked example."
public class NumberRegressionTests
{
    [Test]
    public async Task Number_Arithmetic_UnchangedUnderItemInheritance()
    {
        // 1 + 1, 2 * 3, 10 / 2 — through the same Number.@this paths as before.
        await Assert.That((Number.From(1) + Number.From(1)).ToInt64()).IsEqualTo(2L);
        await Assert.That((Number.From(2) * Number.From(3)).ToInt64()).IsEqualTo(6L);
        await Assert.That((Number.From(10) / Number.From(2)).ToInt64()).IsEqualTo(5L);
    }

    [Test]
    public async Task Number_Compare_UnchangedUnderItemInheritance()
    {
        // Order(1, 2) < 0, AreEqual(5, 5) true — IOrderableValue/IEquatableValue
        // dispatch still routes; `item` adds nothing to ordering.
        await Assert.That(Number.From(1).CompareTo(Number.From(2))).IsLessThan(0);
        await Assert.That(CompareTestOps.Eq(Number.From(5), Number.From(5))).IsTrue();
        await Assert.That(CompareTestOps.OrdD(new Data("", Number.From(1)), new Data("", Number.From(2)))).IsLessThan(0);
    }

    [Test]
    public async Task Number_Truthiness_ZeroFalsyNonZeroTruthy()
    {
        // Routed through `item`'s sync truthiness path (no async hop for a hot if %n%).
        // 0 falsy; 1 truthy; -1 truthy.
        global::app.type.item.@this zero = Number.From(0);
        global::app.type.item.@this one = Number.From(1);
        global::app.type.item.@this neg = Number.From(-1);
        await Assert.That(zero.IsTruthy()).IsFalse();
        await Assert.That(one.IsTruthy()).IsTrue();
        await Assert.That(neg.IsTruthy()).IsTrue();
    }
}
