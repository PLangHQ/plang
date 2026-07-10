using number = global::app.type.item.number.@this;
using PKind = global::app.type.item.number.NumberKind;

namespace PLang.Tests.App.Types;

// plang-types — Stage 4
// Power leaves the integer track on negative or fractional exponents.
// 2^10 → 1024 (integer); 2^-1 → 0.5; 2^0.5 → double.

public class NumberPowerTests
{
    [Test] public async Task Power_TwoPowTen_ReturnsKindInt_1024()
    {
        var r = NumberOps.Power(((number)(2)), ((number)(10)), NumberOps.Lenient);
        await Assert.That(r.Kind.Name).IsEqualTo("int");
        await Assert.That(((global::app.type.item.number.@this)r).Clr<int>()).IsEqualTo(1024);
    }

    [Test] public async Task Power_TwoPowNegOne_ReturnsHalf_NotZero()
    {
        var r = NumberOps.Power(((number)(2)), ((number)(-1)), NumberOps.Lenient);
        // Leaves integer track — returns 0.5 (as double under lenient).
        await Assert.That(r.Kind.Name == "double" || r.Kind.Name == "decimal").IsTrue();
        await Assert.That(((global::app.type.item.number.@this)r).Clr<double>()).IsEqualTo(0.5);
    }

    [Test] public async Task Power_TwoPowHalf_PromotesToDouble()
    {
        var r = NumberOps.Power(((number)(2)), ((number)(0.5)), NumberOps.Lenient);
        await Assert.That(r.Kind.Name).IsEqualTo("double");
    }

    [Test] public async Task Power_LargeExponent_Overflow_PromoteMode_Widens()
    {
        // 2^40 overflows int → widens to long; under Promote.
        var r = NumberOps.Power(((number)(2)), ((number)(40)), NumberOps.Lenient);
        // long range or decimal — past int.
        await Assert.That(r.Kind.Name == "long" || r.Kind.Name == "decimal").IsTrue();
    }

    [Test] public async Task Power_LargeExponent_Overflow_ThrowMode_DataFailMathOverflow()
    {
        var ex = await Assert.That(() => NumberOps.Power(((number)(int.MaxValue)), ((number)(10)),
            (number.Overflow.Throw, number.Precision.Double))).Throws<global::app.error.AppException>();
        await Assert.That(ex!.Key).IsEqualTo("MathOverflow");
    }

    [Test] public async Task Power_ExponentAtCap_SmallBase_StillSucceeds()
    {
        // Boundary: |exp| == MaxPowerExponent is allowed. 1^64 = 1 (no overflow).
        var r = NumberOps.Power(((number)(1)), ((number)(number.MaxPowerExponent)),
            NumberOps.Lenient);
    }

    [Test] public async Task Power_ExponentJustOverCap_TypedFailure_PowerExponentTooLarge()
    {
        // CPU-DoS guard: untrusted exponent above the cap surfaces a typed
        // Data.Fail instead of spinning the actor's core.
        var ex = await Assert.That(() => NumberOps.Power(((number)(2)), ((number)(number.MaxPowerExponent + 1)),
            NumberOps.Lenient)).Throws<global::app.error.AppException>();
        await Assert.That(ex!.Key).IsEqualTo("PowerExponentTooLarge");
    }

    [Test] public async Task Power_NegativeExponentBeyondCap_DecimalPrecision_TypedFailure()
    {
        // Negative integer exponent on integer base lands in the Decimal loop
        // path only when Precision == Decimal. (Way 3: Strict's precision is now
        // Error, so the explicit Precision=Decimal override selects the loop.)
        // Other precision modes route through Math.Pow which is constant-time
        // and skips the cap.
        var ex = await Assert.That(() => NumberOps.Power(((number)(2)), ((number)(-number.MaxPowerExponent - 1)),
            (number.Overflow.Promote, number.Precision.Decimal))).Throws<global::app.error.AppException>();
        await Assert.That(ex!.Key).IsEqualTo("PowerExponentTooLarge");
    }

    [Test] public async Task Power_DoubleBase_LargeExponent_SkipsCap_UsesMathPow()
    {
        // Cap is loop-protective only — Double base routes through Math.Pow
        // (constant time) and is not subject to the magnitude limit.
        var r = NumberOps.Power(((number)(2.0)), ((number)(1000)),
            NumberOps.Lenient);
        await Assert.That(r.Kind.Name).IsEqualTo("double");
    }

    [Test] public async Task Power_NegativeExponent_DoubleBase_SkipsCap()
    {
        var r = NumberOps.Power(((number)(2)), ((number)(-1000)),
            NumberOps.Lenient);
    }

    [Test] public async Task Power_FractionalExponent_NotSubjectToCap()
    {
        // Fractional exponent routes through Math.Pow (constant time) —
        // the cap is integer-loop-only. A genuinely fractional exponent
        // larger than the cap still succeeds without spinning a loop.
        var r = NumberOps.Power(((number)(2)), ((number)(100.5)),
            NumberOps.Lenient);
        await Assert.That(r.Kind.Name).IsEqualTo("double");
    }
}
