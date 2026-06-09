using System.Numerics;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using number = global::app.type.number.@this;
using PKind = global::app.type.number.NumberKind;
using PPolicy = global::app.type.number.NumberPolicy;
using POverflow = global::app.type.number.OverflowMode;
using PPrecision = global::app.type.number.PrecisionMode;

namespace PLang.Tests.App.LazyDeserialize.NumberTowerTests;

// Promote-then-narrow (Decision 5 / Way 3, with the architect's repurposed
// NumberPolicy): integers → BigInteger carrier → narrow (signed-biased);
// double⊕decimal errors by default.
public class NumberArithmeticTests
{
    private static PPolicy Lenient => PPolicy.Lenient;

    [Test] public async Task IntPlusInt_StaysInt()
    {
        var r = number.Add(number.From(2), number.From(3), Lenient);
        await r.IsSuccess();
        await Assert.That((await r.Value())!.Kind).IsEqualTo(PKind.Int);
    }

    // Marquee no-wrap row.
    [Test] public async Task UIntPlusUInt_PromotesAndNarrowsToLong_NoWrap()
    {
        var r = number.Add(number.From(3000000000u), number.From(2000000000u), Lenient);
        await r.IsSuccess();
        await Assert.That((await r.Value())!.Kind).IsEqualTo(PKind.Long);
        await Assert.That((await r.Value())!.ToInt64()).IsEqualTo(5000000000L);
    }

    [Test] public async Task IntPlusFloat_PromotesToDouble()
    {
        var r = number.Add(number.From(2), number.From(1.5f), Lenient);
        await r.IsSuccess();
        await Assert.That((await r.Value())!.Kind).IsEqualTo(PKind.Double);
    }

    [Test] public async Task IntPlusDecimal_PromotesToDecimal()
    {
        var r = number.Add(number.From(2), number.From(1.5m), Lenient);
        await r.IsSuccess();
        await Assert.That((await r.Value())!.Kind).IsEqualTo(PKind.Decimal);
    }

    // The "correct not easy" edge — double⊕decimal needs an explicit precision.
    [Test] public async Task DoublePlusDecimal_RaisesExplicitCastError()
    {
        var r = number.Add(number.From(1.5d), number.From(0.1m), Lenient);
        await r.IsFailure();
        await Assert.That(r.Error?.Key).IsEqualTo("PrecisionMixRequiresChoice");
    }

    [Test] public async Task DivisionProducingFraction_LandsOnDecimalOrDouble_PerOperandKinds()
    {
        var dec = number.Divide(number.From(7), number.From(2), Lenient);
        await dec.IsSuccess();
        await Assert.That((await dec.Value())!.Kind).IsEqualTo(PKind.Decimal); // 7/2 → 3.5 (decimal)
        var dbl = number.Divide(number.From(7.0), number.From(2), Lenient);
        await dbl.IsSuccess();
        await Assert.That((await dbl.Value())!.Kind).IsEqualTo(PKind.Double);
    }

    [Test] public async Task BigIntegerLossless_AcrossSumOfManyInts()
    {
        BigInteger huge = (BigInteger)Int128.MaxValue + 1; // beyond Int128 → BigInteger
        var r = number.Add(number.From(huge), number.From(1), Lenient);
        await r.IsSuccess();
        await Assert.That((await r.Value())!.Kind).IsEqualTo(PKind.BigInteger);
        await Assert.That((await r.Value())!.ToBigInteger()).IsEqualTo(huge + 1);
    }

    // Independent #10 — narrow only when the value overflows; decimal stays decimal.
    [Test] public async Task Narrowing_OnlyWhenValueFits()
    {
        var r = number.Add(number.From(0.1m), number.From(10m), Lenient);
        await r.IsSuccess();
        await Assert.That((await r.Value())!.Kind).IsEqualTo(PKind.Decimal);
        await Assert.That((await r.Value())!.ToDecimal()).IsEqualTo(10.1m);
    }

    // Independent #11 — integer-tower associativity.
    [Test] public async Task IntegerAssociativity_AcrossKinds()
    {
        var ab_c = number.Add((await number.Add(number.From(2000000000u), number.From(2000000000u), Lenient).Value())!,
                              number.From(2000000000u), Lenient);
        var a_bc = number.Add(number.From(2000000000u),
                              (await number.Add(number.From(2000000000u), number.From(2000000000u), Lenient).Value())!, Lenient);
        await ab_c.IsSuccess();
        await a_bc.IsSuccess();
        await Assert.That((await ab_c.Value())!.ToBigInteger()).IsEqualTo((await a_bc.Value())!.ToBigInteger());
        await Assert.That((await ab_c.Value())!.ToBigInteger()).IsEqualTo((BigInteger)6000000000);
    }
}
