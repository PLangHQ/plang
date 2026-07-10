using number = global::app.type.item.number.@this;
using PKind = global::app.type.item.number.NumberKind;

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
        var r = NumberOps.Add(((number)(2)), ((number)(3)), Lenient);
        await Assert.That(r.Kind.Name).IsEqualTo("int");
    }

    [Test] public async Task Add_IntLong_ReturnsLong()
        => await Assert.That(NumberOps.Add(((number)(2)), ((number)(3L)), Lenient).Kind.Name).IsEqualTo("long");

    [Test] public async Task Add_IntDecimal_ReturnsDecimal()
        => await Assert.That(NumberOps.Add(((number)(2)), ((number)(3m)), Lenient).Kind.Name).IsEqualTo("decimal");

    [Test] public async Task Add_AnythingDouble_ReturnsDouble()
        => await Assert.That(NumberOps.Add(((number)(2)), ((number)(3.0)), Lenient).Kind.Name).IsEqualTo("double");

    [Test] public async Task Mul_DecimalDouble_PrecisionEqualsDouble_ReturnsDouble()
        // Way 3: decimal⊕double under the DEFAULT (Lenient) precision errors —
        // the developer must choose. The Double result needs the explicit override.
        => await Assert.That(NumberOps.Multiply(((number)(2m)), ((number)(3.0)),
            (number.Overflow.Promote, number.Precision.Double))!.Kind.Name).IsEqualTo("double");

    [Test] public async Task Mul_DecimalDouble_PrecisionEqualsDecimal_ReturnsDecimal()
    {
        var r = NumberOps.Multiply(((number)(2m)), ((number)(3.0)),
            (number.Overflow.Promote, number.Precision.Decimal));
        await Assert.That(r.Kind.Name).IsEqualTo("decimal");
    }

    [Test] public async Task Overflow_Promote_IntOverflowWidensToLong()
    {
        var r = NumberOps.Add(((number)(int.MaxValue)), ((number)(int.MaxValue)), Lenient);
        await Assert.That(r.Kind.Name).IsEqualTo("long");
    }

    [Test] public async Task Overflow_Promote_LongOverflowWidensToInt128()
    {
        // Way 3: long+long overflow widens along the signed track to Int128
        // (BigInteger carrier → narrow), never wraps. (Was Decimal pre-Way-3.)
        var r = NumberOps.Add(((number)(long.MaxValue)), ((number)(long.MaxValue)), Lenient);
        await Assert.That(r.Kind.Name).IsEqualTo("int128");
    }

    [Test] public async Task Overflow_Throw_IntPlusInt_SurfacesDataFailMathOverflow()
    {
        var ex = await Assert.That(() => NumberOps.Add(((number)(int.MaxValue)), ((number)(int.MaxValue)), Strict)).Throws<global::app.error.AppException>();
        await Assert.That(ex!.Key).IsEqualTo("MathOverflow");
    }

    [Test] public async Task Overflow_Throw_HandlerPathReturnsDataError_NotException()
    {
        await Assert.That(() => { var _ = ((number)(decimal.MaxValue)) + ((number)(decimal.MaxValue)); })
            .Throws<System.OverflowException>();
        var ex = await Assert.That(() => NumberOps.Add(((number)(decimal.MaxValue)), ((number)(decimal.MaxValue)), Strict)).Throws<global::app.error.AppException>();
        await Assert.That(ex!.Key).IsEqualTo("MathOverflow");
    }

    [Test] public async Task Sub_IntInt_ReturnsInt()
        => await Assert.That(NumberOps.Subtract(((number)(7)), ((number)(2)), Lenient).Kind.Name).IsEqualTo("int");

    [Test] public async Task Mod_IntInt_ReturnsInt()
        => await Assert.That(NumberOps.Modulo(((number)(7)), ((number)(3)), Lenient).Kind.Name).IsEqualTo("int");
}
