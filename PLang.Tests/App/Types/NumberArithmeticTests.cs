namespace PLang.Tests.App.Types;

// plang-types — Stage 4
// this.Arithmetic.cs — policy-aware Add/Sub/Mul/Mod. Returns Data<number> (catches internally).
// Promotion: int+int→int, int+long→long, int+decimal→decimal, anything+double→double.
// Overflow=Promote widens (int→long→decimal); Overflow=Throw surfaces as Data.Fail("MathOverflow").

public class NumberArithmeticTests
{
    [Test] public async Task Add_IntInt_ReturnsInt()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Add_IntLong_ReturnsLong()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Add_IntDecimal_ReturnsDecimal()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Add_AnythingDouble_ReturnsDouble()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Mul_DecimalDouble_PrecisionEqualsDouble_ReturnsDouble()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Mul_DecimalDouble_PrecisionEqualsDecimal_ReturnsDecimal()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Overflow_Promote_IntOverflowWidensToLong()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Overflow_Promote_LongOverflowWidensToDecimal()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Overflow_Throw_IntPlusInt_SurfacesDataFailMathOverflow()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Overflow_Throw_HandlerPathReturnsDataError_NotException()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Sub_IntInt_ReturnsInt()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Mod_IntInt_ReturnsInt()
        => throw new global::System.NotImplementedException();
}
