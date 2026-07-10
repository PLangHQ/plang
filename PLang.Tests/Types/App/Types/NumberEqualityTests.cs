using number = global::app.type.item.number.@this;

namespace PLang.Tests.App.Types;

// plang-types — Stage 3
// Equality is lenient by default; ExactEquals is opt-in for crypto/finance.
// Cross-kind lenient == is NOT transitive at the precision boundary — documented, not a bug.
// NaN never equals NaN. Canonical GetHashCode: integer-valued kinds share a bucket.

public class NumberEqualityTests
{
    [Test] public async Task LenientEquals_Int5_LongFiveL_True()
        => await Assert.That(((number)(5)).Equals(((number)(5L)))).IsTrue();

    [Test] public async Task LenientEquals_DecimalFiveM_DoubleFivePoint0_True()
        => await Assert.That(((number)(5m)).Equals(((number)(5.0)))).IsTrue();

    [Test] public async Task LenientEquals_NonTransitive_AtPrecisionBoundary_Documented()
    {
        // 1/3 in decimal has 28 sig digits; in double has ~15 — the values
        // diverge past the 15th digit, so the cross-precision compare returns
        // false. Documented non-transitivity from plan/storage.md.
        var decThird = ((number)(1m / 3m));
        var dblThird = ((number)(1.0 / 3.0));
        await Assert.That(decThird.Equals(dblThird)).IsFalse();
    }

    [Test] public async Task ExactEquals_Decimal0Point1_Double0Point1_False()
        => await Assert.That(((number)(0.1m)).ExactEquals(((number)(0.1)))).IsFalse();

    [Test] public async Task ExactEquals_SameKindSameBits_True()
    {
        await Assert.That(((number)(5)).ExactEquals(((number)(5)))).IsTrue();
        await Assert.That(((number)(0.1m)).ExactEquals(((number)(0.1m)))).IsTrue();
    }

    [Test] public async Task LenientEquals_NaN_NaN_False()
        => await Assert.That(((number)(double.NaN)).Equals(((number)(double.NaN)))).IsFalse();

    [Test] public async Task ExactEquals_NaN_NaN_False()
        => await Assert.That(((number)(double.NaN)).ExactEquals(((number)(double.NaN)))).IsFalse();

    [Test] public async Task GetHashCode_IntFive_LongFiveL_DecimalFiveM_ShareBucket()
    {
        var h1 = ((number)(5)).GetHashCode();
        var h2 = ((number)(5L)).GetHashCode();
        var h3 = ((number)(5m)).GetHashCode();
        var h4 = ((number)(5.0)).GetHashCode();
        await Assert.That(h1).IsEqualTo(h2);
        await Assert.That(h2).IsEqualTo(h3);
        await Assert.That(h3).IsEqualTo(h4);
    }

    [Test] public async Task GetHashCode_DecimalNonIntegerValued_DoesNotBucketWithInt()
    {
        var h1 = ((number)(5)).GetHashCode();
        var h2 = ((number)(5.5m)).GetHashCode();
        await Assert.That(h1).IsNotEqualTo(h2);
    }

    [Test] public async Task LenientEquals_NeverThrows_OnDecimalDoubleCross()
    {
        // Even when the double is out of decimal range / Infinity, equality
        // must not propagate the exception out.
        await Assert.That(((number)(5m)).Equals(((number)(double.PositiveInfinity)))).IsFalse();
        await Assert.That(((number)(5m)).Equals(((number)(double.MaxValue)))).IsFalse();
    }
}
