using number = global::app.type.item.number.@this;
using PKind = global::app.type.item.number.NumberKind;

namespace PLang.Tests.App.Types;

// plang-types — Stage 4 (the divide footgun call)
// Divide leaves the integer track: 7/2 → 3.5 (lenient). Truncating division is math.intdiv.
// Divide-by-zero (integer or decimal) surfaces as Data.Fail("DivideByZero") at the handler;
// internals throw.

public class NumberDivideTests
{
    private static (number.Overflow o, number.Precision p) P => NumberOps.Lenient;

    [Test] public async Task Divide_SevenByTwo_ReturnsThreeAndHalf_NotThree()
    {
        var r = NumberOps.Divide(((number)(7)), ((number)(2)), P);
        await Assert.That(((global::app.type.item.number.@this)r).Clr<decimal>()).IsEqualTo(3.5m);
    }

    [Test] public async Task Divide_IntByInt_LeavesIntegerTrack_KindDecimal()
        => await Assert.That(NumberOps.Divide(((number)(7)), ((number)(2)), P).Kind.Name).IsEqualTo("decimal");

    [Test] public async Task Divide_DecimalByInt_ReturnsDecimal()
        => await Assert.That(NumberOps.Divide(((number)(7m)), ((number)(2)), P).Kind.Name).IsEqualTo("decimal");

    [Test] public async Task Divide_OneByMillion_FullPrecision_NotSilentZero()
    {
        var r = NumberOps.Divide(((number)(1)), ((number)(1000000)), P);
        await Assert.That(((global::app.type.item.number.@this)r).Clr<decimal>()).IsEqualTo(0.000001m);
    }

    [Test] public async Task IntDiv_SevenByTwo_ReturnsThree()
    {
        var r = NumberOps.IntDivide(((number)(7)), ((number)(2)), P);
        await Assert.That(((global::app.type.item.number.@this)r).Clr<int>()).IsEqualTo(3);
    }

    [Test] public async Task IntDiv_NegativeNumerator_TruncatesTowardZero()
    {
        var r = NumberOps.IntDivide(((number)(-7)), ((number)(2)), P);
        await Assert.That(((global::app.type.item.number.@this)r).Clr<int>()).IsEqualTo(-3);
    }

    [Test] public async Task Divide_ByZero_Integer_DataFailDivideByZero()
    {
        var ex = await Assert.That(() => NumberOps.Divide(((number)(7)), ((number)(0)), P)).Throws<global::app.error.AppException>();
        await Assert.That(ex!.Key).IsEqualTo("DivideByZero");
    }

    [Test] public async Task Divide_ByZero_Decimal_DataFailDivideByZero()
    {
        var ex = await Assert.That(() => NumberOps.Divide(((number)(7m)), ((number)(0m)), P)).Throws<global::app.error.AppException>();
        await Assert.That(ex!.Key).IsEqualTo("DivideByZero");
    }

    [Test] public async Task IntDiv_ByZero_DataFailDivideByZero()
    {
        var ex = await Assert.That(() => NumberOps.IntDivide(((number)(7)), ((number)(0)), P)).Throws<global::app.error.AppException>();
        await Assert.That(ex!.Key).IsEqualTo("DivideByZero");
    }
}
