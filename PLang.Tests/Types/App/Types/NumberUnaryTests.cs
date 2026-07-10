using number = global::app.type.item.number.@this;
using PKind = global::app.type.item.number.NumberKind;

namespace PLang.Tests.App.Types;

// plang-types — MathHelper deletion follow-up.
// Pins behavior of number.Abs / Floor / Ceiling / Sqrt / Round / Min / Max —
// the new home for what MathHelper.ToDouble + PreserveType used to do, now
// kind-preserving and policy-aware on min/max.

public class NumberUnaryTests
{
    [Test] public async Task Abs_Int_PreservesIntKind()
    {
        var r = number.Abs(number.From(-5));
        await Assert.That(r.Kind).IsEqualTo(PKind.Int);
        await Assert.That(((global::app.type.item.number.@this)r).Clr<int>()).IsEqualTo(5);
    }

    [Test] public async Task Abs_IntMinValue_PromotesToLong()
    {
        // System.Math.Abs(int.MinValue) throws; the unary surface lifts to Long
        // so the value is representable.
        var r = number.Abs(number.From(int.MinValue));
        await Assert.That(r.Kind).IsEqualTo(PKind.Long);
        await Assert.That(((global::app.type.item.number.@this)r).Clr<long>()).IsEqualTo(-(long)int.MinValue);
    }

    [Test] public async Task Abs_LongMinValue_WidensToInt128()
    {
        // Way 3: abs(long.MinValue) = 9223372036854775808 exceeds the long range,
        // so it widens along the signed track to Int128 rather than overflowing.
        // (Was MathOverflow pre-Way-3.)
        var r = number.Abs(number.From(long.MinValue));
        await Assert.That(r.Kind).IsEqualTo(PKind.Int128);
    }

    [Test] public async Task Abs_Decimal_PreservesDecimalKind()
    {
        var r = number.Abs(number.From(-3.14m));
        await Assert.That(r.Kind).IsEqualTo(PKind.Decimal);
        await Assert.That(((global::app.type.item.number.@this)r).Clr<decimal>()).IsEqualTo(3.14m);
    }

    [Test] public async Task Floor_Int_Unchanged()
    {
        var r = number.Floor(number.From(7));
        await Assert.That(r.Kind).IsEqualTo(PKind.Int);
        await Assert.That(((global::app.type.item.number.@this)r).Clr<int>()).IsEqualTo(7);
    }

    [Test] public async Task Floor_Decimal_RoundsDown()
    {
        var r = number.Floor(number.From(3.9m));
        await Assert.That(r.Kind).IsEqualTo(PKind.Decimal);
        await Assert.That(((global::app.type.item.number.@this)r).Clr<decimal>()).IsEqualTo(3m);
    }

    [Test] public async Task Ceiling_Double_RoundsUp()
    {
        var r = number.Ceiling(number.From(3.1));
        await Assert.That(r.Kind).IsEqualTo(PKind.Double);
        await Assert.That(((global::app.type.item.number.@this)r).Clr<double>()).IsEqualTo(4.0);
    }

    [Test] public async Task Sqrt_PositiveInt_ReturnsDouble()
    {
        var r = number.Sqrt(number.From(16));
        await Assert.That(r.Kind).IsEqualTo(PKind.Double);
        await Assert.That(((global::app.type.item.number.@this)r).Clr<double>()).IsEqualTo(4.0);
    }

    [Test] public async Task Sqrt_NegativeNumber_SurfacesArithmeticError()
    {
        // number.Sqrt throws ArithmeticException → Wrap maps to "ArithmeticError"
        // key. math.sqrt handler relies on this — no pre-check, one canonical
        // error key for negative-sqrt across both call paths.
        var ex = await Assert.That(() => number.Sqrt(number.From(-1))).Throws<global::app.error.AppException>();
        await Assert.That(ex!.Key).IsEqualTo("ArithmeticError");
    }

    [Test] public async Task Round_DecimalToTwoPlaces_AwayFromZero()
    {
        var r = number.Round(number.From(2.345m), 2);
        await Assert.That(((global::app.type.item.number.@this)r).Clr<decimal>()).IsEqualTo(2.35m);
    }

    [Test] public async Task Round_Int_Unchanged()
    {
        var r = number.Round(number.From(7), 2);
        await Assert.That(r.Kind).IsEqualTo(PKind.Int);
    }

    [Test] public async Task Min_TwoInts_ReturnsSmallerSameKind()
    {
        var r = NumberOps.Min(number.From(3), number.From(5), NumberOps.Lenient);
        await Assert.That(r.Kind).IsEqualTo(PKind.Int);
        await Assert.That(((global::app.type.item.number.@this)r).Clr<int>()).IsEqualTo(3);
    }

    [Test] public async Task Max_IntAndDecimal_PromotesToDecimal()
    {
        var r = NumberOps.Max(number.From(2), number.From(3.5m), NumberOps.Lenient);
        await Assert.That(r.Kind).IsEqualTo(PKind.Decimal);
        await Assert.That(((global::app.type.item.number.@this)r).Clr<decimal>()).IsEqualTo(3.5m);
    }

    [Test] public async Task Max_NegativeAndPositive_ReturnsPositive()
    {
        var r = NumberOps.Max(number.From(-10), number.From(5), NumberOps.Lenient);
        await Assert.That(((global::app.type.item.number.@this)r).Clr<int>()).IsEqualTo(5);
    }
}
