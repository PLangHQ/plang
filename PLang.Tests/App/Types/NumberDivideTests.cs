using number = global::app.type.number.@this;
using PKind = global::app.type.number.NumberKind;
using PPolicy = global::app.type.number.NumberPolicy;

namespace PLang.Tests.App.Types;

// plang-types — Stage 4 (the divide footgun call)
// Divide leaves the integer track: 7/2 → 3.5 (lenient). Truncating division is math.intdiv.
// Divide-by-zero (integer or decimal) surfaces as Data.Fail("DivideByZero") at the handler;
// internals throw.

public class NumberDivideTests
{
    private static PPolicy P => PPolicy.Lenient;

    [Test] public async Task Divide_SevenByTwo_ReturnsThreeAndHalf_NotThree()
    {
        var r = number.Divide(number.From(7), number.From(2), P);
        await r.IsSuccess();
        await Assert.That((decimal)(await r.Value())!).IsEqualTo(3.5m);
    }

    [Test] public async Task Divide_IntByInt_LeavesIntegerTrack_KindDecimal()
        => await Assert.That((await number.Divide(number.From(7), number.From(2), P).Value())!.Kind).IsEqualTo(PKind.Decimal);

    [Test] public async Task Divide_DecimalByInt_ReturnsDecimal()
        => await Assert.That((await number.Divide(number.From(7m), number.From(2), P).Value())!.Kind).IsEqualTo(PKind.Decimal);

    [Test] public async Task Divide_OneByMillion_FullPrecision_NotSilentZero()
    {
        var r = number.Divide(number.From(1), number.From(1000000), P);
        await r.IsSuccess();
        await Assert.That((decimal)(await r.Value())!).IsEqualTo(0.000001m);
    }

    [Test] public async Task IntDiv_SevenByTwo_ReturnsThree()
    {
        var r = number.IntDivide(number.From(7), number.From(2), P);
        await r.IsSuccess();
        await Assert.That((int)(await r.Value())!).IsEqualTo(3);
    }

    [Test] public async Task IntDiv_NegativeNumerator_TruncatesTowardZero()
    {
        var r = number.IntDivide(number.From(-7), number.From(2), P);
        await Assert.That((int)(await r.Value())!).IsEqualTo(-3);
    }

    [Test] public async Task Divide_ByZero_Integer_DataFailDivideByZero()
    {
        var r = number.Divide(number.From(7), number.From(0), P);
        await r.IsFailure();
        await Assert.That(r.Error?.Key).IsEqualTo("DivideByZero");
    }

    [Test] public async Task Divide_ByZero_Decimal_DataFailDivideByZero()
    {
        var r = number.Divide(number.From(7m), number.From(0m), P);
        await r.IsFailure();
        await Assert.That(r.Error?.Key).IsEqualTo("DivideByZero");
    }

    [Test] public async Task IntDiv_ByZero_DataFailDivideByZero()
    {
        var r = number.IntDivide(number.From(7), number.From(0), P);
        await r.IsFailure();
        await Assert.That(r.Error?.Key).IsEqualTo("DivideByZero");
    }
}
