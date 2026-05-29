using number = global::app.type.number.@this;
using PKind = global::app.type.number.NumberKind;
using PPolicy = global::app.type.number.NumberPolicy;
using POverflow = global::app.type.number.OverflowMode;
using PPrecision = global::app.type.number.PrecisionMode;

namespace PLang.Tests.App.Types;

// plang-types — Stage 4
// this.Arithmetic.cs — policy-aware Add/Sub/Mul/Mod. Returns Data<number> (catches internally).
// Promotion: int+int→int, int+long→long, int+decimal→decimal, anything+double→double.
// Overflow=Promote widens (int→long→decimal); Overflow=Throw surfaces as Data.Fail("MathOverflow").

public class NumberArithmeticTests
{
    private static PPolicy Lenient => PPolicy.Lenient;
    private static PPolicy Strict => PPolicy.Strict;

    [Test] public async Task Add_IntInt_ReturnsInt()
    {
        var r = number.Add(number.From(2), number.From(3), Lenient);
        await Assert.That(r.Success).IsTrue();
        await Assert.That(r.Value!.Kind).IsEqualTo(PKind.Int);
    }

    [Test] public async Task Add_IntLong_ReturnsLong()
        => await Assert.That(number.Add(number.From(2), number.From(3L), Lenient).Value!.Kind).IsEqualTo(PKind.Long);

    [Test] public async Task Add_IntDecimal_ReturnsDecimal()
        => await Assert.That(number.Add(number.From(2), number.From(3m), Lenient).Value!.Kind).IsEqualTo(PKind.Decimal);

    [Test] public async Task Add_AnythingDouble_ReturnsDouble()
        => await Assert.That(number.Add(number.From(2), number.From(3.0), Lenient).Value!.Kind).IsEqualTo(PKind.Double);

    [Test] public async Task Mul_DecimalDouble_PrecisionEqualsDouble_ReturnsDouble()
        => await Assert.That(number.Multiply(number.From(2m), number.From(3.0), Lenient).Value!.Kind).IsEqualTo(PKind.Double);

    [Test] public async Task Mul_DecimalDouble_PrecisionEqualsDecimal_ReturnsDecimal()
    {
        var r = number.Multiply(number.From(2m), number.From(3.0),
            new PPolicy { Overflow = POverflow.Promote, Precision = PPrecision.Decimal });
        await Assert.That(r.Value!.Kind).IsEqualTo(PKind.Decimal);
    }

    [Test] public async Task Overflow_Promote_IntOverflowWidensToLong()
    {
        var r = number.Add(number.From(int.MaxValue), number.From(int.MaxValue), Lenient);
        await Assert.That(r.Success).IsTrue();
        await Assert.That(r.Value!.Kind).IsEqualTo(PKind.Long);
    }

    [Test] public async Task Overflow_Promote_LongOverflowWidensToDecimal()
    {
        var r = number.Add(number.From(long.MaxValue), number.From(long.MaxValue), Lenient);
        await Assert.That(r.Success).IsTrue();
        await Assert.That(r.Value!.Kind).IsEqualTo(PKind.Decimal);
    }

    [Test] public async Task Overflow_Throw_IntPlusInt_SurfacesDataFailMathOverflow()
    {
        var r = number.Add(number.From(int.MaxValue), number.From(int.MaxValue), Strict);
        await Assert.That(r.Success).IsFalse();
        await Assert.That(r.Error?.Key).IsEqualTo("MathOverflow");
    }

    [Test] public async Task Overflow_Throw_HandlerPathReturnsDataError_NotException()
    {
        await Assert.That(() => { var _ = number.From(decimal.MaxValue) + number.From(decimal.MaxValue); })
            .Throws<System.OverflowException>();
        var r = number.Add(number.From(decimal.MaxValue), number.From(decimal.MaxValue), Strict);
        await Assert.That(r.Success).IsFalse();
        await Assert.That(r.Error?.Key).IsEqualTo("MathOverflow");
    }

    [Test] public async Task Sub_IntInt_ReturnsInt()
        => await Assert.That(number.Subtract(number.From(7), number.From(2), Lenient).Value!.Kind).IsEqualTo(PKind.Int);

    [Test] public async Task Mod_IntInt_ReturnsInt()
        => await Assert.That(number.Modulo(number.From(7), number.From(3), Lenient).Value!.Kind).IsEqualTo(PKind.Int);
}
