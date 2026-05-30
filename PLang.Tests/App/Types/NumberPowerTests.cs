using number = global::app.type.number.@this;
using PKind = global::app.type.number.NumberKind;
using PPolicy = global::app.type.number.NumberPolicy;
using POverflow = global::app.type.number.OverflowMode;
using PPrecision = global::app.type.number.PrecisionMode;

namespace PLang.Tests.App.Types;

// plang-types — Stage 4
// Power leaves the integer track on negative or fractional exponents.
// 2^10 → 1024 (integer); 2^-1 → 0.5; 2^0.5 → double.

public class NumberPowerTests
{
    [Test] public async Task Power_TwoPowTen_ReturnsKindInt_1024()
    {
        var r = number.Power(number.From(2), number.From(10), PPolicy.Lenient);
        await Assert.That(r.Success).IsTrue();
        await Assert.That(r.Value!.Kind).IsEqualTo(PKind.Int);
        await Assert.That((int)r.Value!).IsEqualTo(1024);
    }

    [Test] public async Task Power_TwoPowNegOne_ReturnsHalf_NotZero()
    {
        var r = number.Power(number.From(2), number.From(-1), PPolicy.Lenient);
        await Assert.That(r.Success).IsTrue();
        // Leaves integer track — returns 0.5 (as double under lenient).
        await Assert.That(r.Value!.Kind == PKind.Double || r.Value!.Kind == PKind.Decimal).IsTrue();
        await Assert.That((double)r.Value!).IsEqualTo(0.5);
    }

    [Test] public async Task Power_TwoPowHalf_PromotesToDouble()
    {
        var r = number.Power(number.From(2), number.From(0.5), PPolicy.Lenient);
        await Assert.That(r.Success).IsTrue();
        await Assert.That(r.Value!.Kind).IsEqualTo(PKind.Double);
    }

    [Test] public async Task Power_LargeExponent_Overflow_PromoteMode_Widens()
    {
        // 2^40 overflows int → widens to long; under Promote.
        var r = number.Power(number.From(2), number.From(40), PPolicy.Lenient);
        await Assert.That(r.Success).IsTrue();
        // long range or decimal — past int.
        await Assert.That(r.Value!.Kind == PKind.Long || r.Value!.Kind == PKind.Decimal).IsTrue();
    }

    [Test] public async Task Power_LargeExponent_Overflow_ThrowMode_DataFailMathOverflow()
    {
        var r = number.Power(number.From(int.MaxValue), number.From(10),
            new PPolicy { Overflow = POverflow.Throw, Precision = PPrecision.Double });
        await Assert.That(r.Success).IsFalse();
        await Assert.That(r.Error?.Key).IsEqualTo("MathOverflow");
    }

    [Test] public async Task Power_ExponentAtCap_SmallBase_StillSucceeds()
    {
        // Boundary: |exp| == MaxPowerExponent is allowed. 1^64 = 1 (no overflow).
        var r = number.Power(number.From(1), number.From(number.MaxPowerExponent),
            PPolicy.Lenient);
        await Assert.That(r.Success).IsTrue();
    }

    [Test] public async Task Power_ExponentJustOverCap_TypedFailure_PowerExponentTooLarge()
    {
        // CPU-DoS guard: untrusted exponent above the cap surfaces a typed
        // Data.Fail instead of spinning the actor's core.
        var r = number.Power(number.From(2), number.From(number.MaxPowerExponent + 1),
            PPolicy.Lenient);
        await Assert.That(r.Success).IsFalse();
        await Assert.That(r.Error?.Key).IsEqualTo("PowerExponentTooLarge");
    }

    [Test] public async Task Power_NegativeExponentBeyondCap_DecimalPrecision_TypedFailure()
    {
        // Negative integer exponent on integer base lands in the Decimal loop
        // path only when Precision == Decimal (Strict). Lenient routes through
        // Math.Pow which is constant-time and skips the cap.
        var r = number.Power(number.From(2), number.From(-number.MaxPowerExponent - 1),
            PPolicy.Strict);
        await Assert.That(r.Success).IsFalse();
        await Assert.That(r.Error?.Key).IsEqualTo("PowerExponentTooLarge");
    }

    [Test] public async Task Power_DoubleBase_LargeExponent_SkipsCap_UsesMathPow()
    {
        // Cap is loop-protective only — Double base routes through Math.Pow
        // (constant time) and is not subject to the magnitude limit.
        var r = number.Power(number.From(2.0), number.From(1000),
            PPolicy.Lenient);
        await Assert.That(r.Success).IsTrue();
        await Assert.That(r.Value!.Kind).IsEqualTo(PKind.Double);
    }

    [Test] public async Task Power_NegativeExponent_DoubleBase_SkipsCap()
    {
        var r = number.Power(number.From(2), number.From(-1000),
            PPolicy.Lenient);
        await Assert.That(r.Success).IsTrue();
    }

    [Test] public async Task Power_FractionalExponent_NotSubjectToCap()
    {
        // Fractional exponent routes through Math.Pow (constant time) —
        // the cap is integer-loop-only. A genuinely fractional exponent
        // larger than the cap still succeeds without spinning a loop.
        var r = number.Power(number.From(2), number.From(100.5),
            PPolicy.Lenient);
        await Assert.That(r.Success).IsTrue();
        await Assert.That(r.Value!.Kind).IsEqualTo(PKind.Double);
    }
}
