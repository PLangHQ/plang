namespace PLang.Tests.App.Types;

// plang-types — Stage 4 (the divide footgun call)
// Divide leaves the integer track: 7/2 → 3.5 (lenient). Truncating division is math.intdiv.
// Divide-by-zero (integer or decimal) surfaces as Data.Fail("DivideByZero") at the handler;
// internals throw.

public class NumberDivideTests
{
    [Test] public async Task Divide_SevenByTwo_ReturnsThreeAndHalf_NotThree()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Divide_IntByInt_LeavesIntegerTrack_KindDecimal()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Divide_DecimalByInt_ReturnsDecimal()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Divide_OneByMillion_FullPrecision_NotSilentZero()
        => throw new global::System.NotImplementedException();

    [Test] public async Task IntDiv_SevenByTwo_ReturnsThree()
        => throw new global::System.NotImplementedException();

    [Test] public async Task IntDiv_NegativeNumerator_TruncatesTowardZero()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Divide_ByZero_Integer_DataFailDivideByZero()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Divide_ByZero_Decimal_DataFailDivideByZero()
        => throw new global::System.NotImplementedException();

    [Test] public async Task IntDiv_ByZero_DataFailDivideByZero()
        => throw new global::System.NotImplementedException();
}
