using System.Numerics;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using number = global::app.type.item.number.@this;
using PKind = global::app.type.item.number.NumberKind;

namespace PLang.Tests.App.LazyDeserialize.NumberTowerTests;

// Promote-then-narrow (Decision 5 / Way 3, with the architect's repurposed
// NumberPolicy): integers → BigInteger carrier → narrow (signed-biased);
// double⊕decimal errors by default.
public class NumberArithmeticTests
{
    private static (number.Overflow o, number.Precision p) Lenient => NumberOps.Lenient;

    [Test] public async Task IntPlusInt_StaysInt()
    {
        var r = NumberOps.Add(((number)(2)), ((number)(3)), Lenient);
        await Assert.That(r.Kind.Name).IsEqualTo("int");
    }

    // Marquee no-wrap row.
    [Test] public async Task UIntPlusUInt_PromotesAndNarrowsToLong_NoWrap()
    {
        var r = NumberOps.Add(((number)(3000000000u)), ((number)(2000000000u)), Lenient);
        await Assert.That(r.Kind.Name).IsEqualTo("long");
        await Assert.That(r.ToInt64()).IsEqualTo(5000000000L);
    }

    [Test] public async Task IntPlusFloat_PromotesToDouble()
    {
        var r = NumberOps.Add(((number)(2)), ((number)(1.5f)), Lenient);
        await Assert.That(r.Kind.Name).IsEqualTo("double");
    }

    [Test] public async Task IntPlusDecimal_PromotesToDecimal()
    {
        var r = NumberOps.Add(((number)(2)), ((number)(1.5m)), Lenient);
        await Assert.That(r.Kind.Name).IsEqualTo("decimal");
    }

    // The "correct not easy" edge — double⊕decimal needs an explicit precision.
    [Test] public async Task DoublePlusDecimal_RaisesExplicitCastError()
    {
        var ex = await Assert.That(() => NumberOps.Add(((number)(1.5d)), ((number)(0.1m)), Lenient)).Throws<global::app.error.AppException>();
        await Assert.That(ex!.Key).IsEqualTo("PrecisionMixRequiresChoice");
    }

    [Test] public async Task DivisionProducingFraction_LandsOnDecimalOrDouble_PerOperandKinds()
    {
        var dec = NumberOps.Divide(((number)(7)), ((number)(2)), Lenient);
        await Assert.That(dec.Kind.Name).IsEqualTo("decimal"); // 7/2 → 3.5 (decimal)
        var dbl = NumberOps.Divide(((number)(7.0)), ((number)(2)), Lenient);
        await Assert.That(dbl.Kind.Name).IsEqualTo("double");
    }

    [Test] public async Task BigIntegerLossless_AcrossSumOfManyInts()
    {
        BigInteger huge = (BigInteger)Int128.MaxValue + 1; // beyond Int128 → BigInteger
        var r = NumberOps.Add(((number)(huge)), ((number)(1)), Lenient);
        await Assert.That(r.Kind.Name).IsEqualTo("biginteger");
        await Assert.That(r.ToBigInteger()).IsEqualTo(huge + 1);
    }

    // Independent #10 — narrow only when the value overflows; decimal stays decimal.
    [Test] public async Task Narrowing_OnlyWhenValueFits()
    {
        var r = NumberOps.Add(((number)(0.1m)), ((number)(10m)), Lenient);
        await Assert.That(r.Kind.Name).IsEqualTo("decimal");
        await Assert.That(r.ToDecimal()).IsEqualTo(10.1m);
    }

    // Independent #11 — integer-tower associativity.
    [Test] public async Task IntegerAssociativity_AcrossKinds()
    {
        var ab_c = NumberOps.Add(NumberOps.Add(((number)(2000000000u)), ((number)(2000000000u)), Lenient),
                              ((number)(2000000000u)), Lenient);
        var a_bc = NumberOps.Add(((number)(2000000000u)),
                              NumberOps.Add(((number)(2000000000u)), ((number)(2000000000u)), Lenient), Lenient);
        await Assert.That(ab_c.ToBigInteger()).IsEqualTo(a_bc.ToBigInteger());
        await Assert.That(ab_c.ToBigInteger()).IsEqualTo((BigInteger)6000000000);
    }
}
