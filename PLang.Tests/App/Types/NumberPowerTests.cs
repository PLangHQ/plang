namespace PLang.Tests.App.Types;

// plang-types — Stage 4
// Power leaves the integer track on negative or fractional exponents.
// 2^10 → 1024 (integer); 2^-1 → 0.5; 2^0.5 → double.

public class NumberPowerTests
{
    [Test] public async Task Power_TwoPowTen_ReturnsKindInt_1024()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Power_TwoPowNegOne_ReturnsHalf_NotZero()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Power_TwoPowHalf_PromotesToDouble()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Power_LargeExponent_Overflow_PromoteMode_Widens()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Power_LargeExponent_Overflow_ThrowMode_DataFailMathOverflow()
        => throw new global::System.NotImplementedException();
}
