using System.Numerics;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using number = global::app.type.item.number.@this;

namespace PLang.Tests.App.LazyDeserialize.NumberTowerTests;

// Stage 2's reader-side parsing. number.Read (== number.Convert) parses a
// string toward its exact named kind — no implicit widening, no silent
// narrowing. (Context is unused for parsing; passed null.)
public class NumberReadTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create("/tmp/numreadtests-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test] public async Task Read_NumberInt_FromString_PreservesInt()
    {
        var r = number.Convert("5", "int", _app.User.Context);
        await Assert.That(((global::app.type.item.number.@this)(await r.Value())!).BoxedValue).IsTypeOf<int>();
        await Assert.That(((global::app.type.item.number.@this)(await r.Value())!).Clr<int>()).IsEqualTo(5);
    }

    [Test] public async Task Read_NumberUInt_FromBigDecimalString_ProducesUInt()
    {
        var r = number.Convert("3000000000", "uint", _app.User.Context);
        await Assert.That(((global::app.type.item.number.@this)(await r.Value())!).BoxedValue).IsTypeOf<uint>();
        await Assert.That(((global::app.type.item.number.@this)(await r.Value())!).Clr<uint>()).IsEqualTo(3000000000u);
        // toward int it overflows → typed error.
        await number.Convert("3000000000", "int", _app.User.Context).IsFailure();
    }

    [Test] public async Task Read_NumberBigInteger_From22DigitString_LossLess()
    {
        const string s = "9999999999999999999999";
        var r = number.Convert(s, "biginteger", _app.User.Context);
        await Assert.That(((global::app.type.item.number.@this)(await r.Value())!).BoxedValue).IsTypeOf<BigInteger>();
        await Assert.That(((global::app.type.item.number.@this)(await r.Value())!).Clr<BigInteger>()).IsEqualTo(BigInteger.Parse(s));
    }

    [Test] public async Task Read_NumberFloat_NegativeZero_PreservesSignAndKind()
    {
        var r = number.Convert("-0.0", "float", _app.User.Context);
        await Assert.That(((global::app.type.item.number.@this)(await r.Value())!).BoxedValue).IsTypeOf<float>();
        await Assert.That(float.IsNegative(((global::app.type.item.number.@this)(await r.Value())!).Clr<float>())).IsTrue();
    }

    [Test] public async Task Read_NumberDecimal_PrecisionPreserved_28Digits()
    {
        const string s = "1.234567890123456789012345678";
        var r = number.Convert(s, "decimal", _app.User.Context);
        await Assert.That(((global::app.type.item.number.@this)(await r.Value())!).BoxedValue).IsTypeOf<decimal>();
        await Assert.That(((global::app.type.item.number.@this)(await r.Value())!).Clr<decimal>()).IsEqualTo(decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Test] public async Task Read_NumberHalf_FromString_PreservesHalf()
    {
        var r = number.Convert("1.5", "half", _app.User.Context);
        await Assert.That(((global::app.type.item.number.@this)(await r.Value())!).BoxedValue).IsTypeOf<Half>();
        await Assert.That(((global::app.type.item.number.@this)(await r.Value())!).Clr<Half>()).IsEqualTo((Half)1.5);
    }

    // Under lazy (Stage 3) this fires at first touch; Stage 2 pins that the
    // too-big-for-kind read produces a typed error rather than wrapping.
    [Test] public async Task Read_TooBigForNamedKind_ErrorsAtMaterialise_NotAtRead()
        => await number.Convert("99999999999999999999999999999999", "int", _app.User.Context).IsFailure();

    [Test] public async Task Read_NonNumericString_ProducesTypedError()
    {
        var r = number.Convert("hello", "int", _app.User.Context);
        await r.IsFailure();
        await Assert.That(r.Error?.Key).IsEqualTo("NumberConversionFailed");
    }
}
