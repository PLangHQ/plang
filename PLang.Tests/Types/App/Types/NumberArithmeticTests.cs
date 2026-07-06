using number = global::app.type.number.@this;
using PKind = global::app.type.number.NumberKind;

namespace PLang.Tests.App.Types;

// plang-types — Stage 4
// this.Arithmetic.cs — policy-aware Add/Sub/Mul/Mod. Returns Data<number> (catches internally).
// Promotion: int+int→int, int+long→long, int+decimal→decimal, anything+double→double.
// Overflow=Promote widens (int→long→decimal); Overflow=Throw surfaces as Data.Fail("MathOverflow").

public class NumberArithmeticTests
{
    private static (number.Overflow o, number.Precision p) Lenient => NumberOps.Lenient;
    private static (number.Overflow o, number.Precision p) Strict => NumberOps.Strict;

    [Test] public async Task Add_IntInt_ReturnsInt()
    {
        var r = NumberOps.Add(number.From(2), number.From(3), Lenient);
        await Assert.That(r.Kind).IsEqualTo(PKind.Int);
    }

    [Test] public async Task Add_IntLong_ReturnsLong()
        => await Assert.That(NumberOps.Add(number.From(2), number.From(3L), Lenient).Kind).IsEqualTo(PKind.Long);

    [Test] public async Task Add_IntDecimal_ReturnsDecimal()
        => await Assert.That(NumberOps.Add(number.From(2), number.From(3m), Lenient).Kind).IsEqualTo(PKind.Decimal);

    [Test] public async Task Add_AnythingDouble_ReturnsDouble()
        => await Assert.That(NumberOps.Add(number.From(2), number.From(3.0), Lenient).Kind).IsEqualTo(PKind.Double);

    [Test] public async Task Mul_DecimalDouble_PrecisionEqualsDouble_ReturnsDouble()
        // Way 3: decimal⊕double under the DEFAULT (Lenient) precision errors —
        // the developer must choose. The Double result needs the explicit override.
        => await Assert.That(NumberOps.Multiply(number.From(2m), number.From(3.0),
            (number.Overflow.Promote, number.Precision.Double))!.Kind).IsEqualTo(PKind.Double);

    [Test] public async Task Mul_DecimalDouble_PrecisionEqualsDecimal_ReturnsDecimal()
    {
        var r = NumberOps.Multiply(number.From(2m), number.From(3.0),
            (number.Overflow.Promote, number.Precision.Decimal));
        await Assert.That(r.Kind).IsEqualTo(PKind.Decimal);
    }

    [Test] public async Task Overflow_Promote_IntOverflowWidensToLong()
    {
        var r = NumberOps.Add(number.From(int.MaxValue), number.From(int.MaxValue), Lenient);
        await Assert.That(r.Kind).IsEqualTo(PKind.Long);
    }

    [Test] public async Task Overflow_Promote_LongOverflowWidensToInt128()
    {
        // Way 3: long+long overflow widens along the signed track to Int128
        // (BigInteger carrier → narrow), never wraps. (Was Decimal pre-Way-3.)
        var r = NumberOps.Add(number.From(long.MaxValue), number.From(long.MaxValue), Lenient);
        await Assert.That(r.Kind).IsEqualTo(PKind.Int128);
    }

    [Test] public async Task Overflow_Throw_IntPlusInt_SurfacesDataFailMathOverflow()
    {
        var ex = await Assert.That(() => NumberOps.Add(number.From(int.MaxValue), number.From(int.MaxValue), Strict)).Throws<global::app.error.AppException>();
        await Assert.That(ex!.Key).IsEqualTo("MathOverflow");
    }

    [Test] public async Task Overflow_Throw_HandlerPathReturnsDataError_NotException()
    {
        await Assert.That(() => { var _ = number.From(decimal.MaxValue) + number.From(decimal.MaxValue); })
            .Throws<System.OverflowException>();
        var ex = await Assert.That(() => NumberOps.Add(number.From(decimal.MaxValue), number.From(decimal.MaxValue), Strict)).Throws<global::app.error.AppException>();
        await Assert.That(ex!.Key).IsEqualTo("MathOverflow");
    }

    [Test] public async Task Sub_IntInt_ReturnsInt()
        => await Assert.That(NumberOps.Subtract(number.From(7), number.From(2), Lenient).Kind).IsEqualTo(PKind.Int);

    [Test] public async Task Mod_IntInt_ReturnsInt()
        => await Assert.That(NumberOps.Modulo(number.From(7), number.From(3), Lenient).Kind).IsEqualTo(PKind.Int);
}
