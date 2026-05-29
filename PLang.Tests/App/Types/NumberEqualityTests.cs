namespace PLang.Tests.App.Types;

// plang-types — Stage 3
// Equality is lenient by default; ExactEquals is opt-in for crypto/finance.
// Cross-kind lenient == is NOT transitive at the precision boundary — documented, not a bug.
// NaN never equals NaN. Canonical GetHashCode: integer-valued kinds share a bucket.

public class NumberEqualityTests
{
    [Test] public async Task LenientEquals_Int5_LongFiveL_True()
        => throw new global::System.NotImplementedException();

    [Test] public async Task LenientEquals_DecimalFiveM_DoubleFivePoint0_True()
        => throw new global::System.NotImplementedException();

    [Test] public async Task LenientEquals_NonTransitive_AtPrecisionBoundary_Documented()
        => throw new global::System.NotImplementedException();

    [Test] public async Task ExactEquals_Decimal0Point1_Double0Point1_False()
        => throw new global::System.NotImplementedException();

    [Test] public async Task ExactEquals_SameKindSameBits_True()
        => throw new global::System.NotImplementedException();

    [Test] public async Task LenientEquals_NaN_NaN_False()
        => throw new global::System.NotImplementedException();

    [Test] public async Task ExactEquals_NaN_NaN_False()
        => throw new global::System.NotImplementedException();

    [Test] public async Task GetHashCode_IntFive_LongFiveL_DecimalFiveM_ShareBucket()
        => throw new global::System.NotImplementedException();

    [Test] public async Task GetHashCode_DecimalNonIntegerValued_DoesNotBucketWithInt()
        => throw new global::System.NotImplementedException();

    [Test] public async Task LenientEquals_NeverThrows_OnDecimalDoubleCross()
        => throw new global::System.NotImplementedException();
}
