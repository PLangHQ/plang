using System.Numerics;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using number = global::app.type.number.@this;

namespace PLang.Tests.App.LazyDeserialize.NumberTowerTests;

// Stage 2's reader-side parsing. number.Read (== number.Convert) parses a
// string toward its exact named kind — no implicit widening, no silent
// narrowing. (Context is unused for parsing; passed null.)
public class NumberReadTests
{
    [Test] public async Task Read_NumberInt_FromString_PreservesInt()
    {
        var r = number.Convert("5", "int", null!);
        await Assert.That(r.Value).IsTypeOf<int>();
        await Assert.That((int)(await r.Value())!).IsEqualTo(5);
    }

    [Test] public async Task Read_NumberUInt_FromBigDecimalString_ProducesUInt()
    {
        var r = number.Convert("3000000000", "uint", null!);
        await Assert.That(r.Value).IsTypeOf<uint>();
        await Assert.That((uint)(await r.Value())!).IsEqualTo(3000000000u);
        // toward int it overflows → typed error.
        await number.Convert("3000000000", "int", null!).IsFailure();
    }

    [Test] public async Task Read_NumberBigInteger_From22DigitString_LossLess()
    {
        const string s = "9999999999999999999999";
        var r = number.Convert(s, "biginteger", null!);
        await Assert.That(r.Value).IsTypeOf<BigInteger>();
        await Assert.That((BigInteger)(await r.Value())!).IsEqualTo(BigInteger.Parse(s));
    }

    [Test] public async Task Read_NumberFloat_NegativeZero_PreservesSignAndKind()
    {
        var r = number.Convert("-0.0", "float", null!);
        await Assert.That(r.Value).IsTypeOf<float>();
        await Assert.That(float.IsNegative((float)(await r.Value())!)).IsTrue();
    }

    [Test] public async Task Read_NumberDecimal_PrecisionPreserved_28Digits()
    {
        const string s = "1.234567890123456789012345678";
        var r = number.Convert(s, "decimal", null!);
        await Assert.That(r.Value).IsTypeOf<decimal>();
        await Assert.That((decimal)(await r.Value())!).IsEqualTo(decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Test] public async Task Read_NumberHalf_FromString_PreservesHalf()
    {
        var r = number.Convert("1.5", "half", null!);
        await Assert.That(r.Value).IsTypeOf<Half>();
        await Assert.That((Half)(await r.Value())!).IsEqualTo((Half)1.5);
    }

    // Under lazy (Stage 3) this fires at first touch; Stage 2 pins that the
    // too-big-for-kind read produces a typed error rather than wrapping.
    [Test] public async Task Read_TooBigForNamedKind_ErrorsAtMaterialise_NotAtRead()
        => await number.Convert("99999999999999999999999999999999", "int", null!).IsFailure();

    [Test] public async Task Read_NonNumericString_ProducesTypedError()
    {
        var r = number.Convert("hello", "int", null!);
        await r.IsFailure();
        await Assert.That(r.Error?.Key).IsEqualTo("NumberConversionFailed");
    }
}
