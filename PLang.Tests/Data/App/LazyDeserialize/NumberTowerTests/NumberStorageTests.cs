using System.Numerics;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using number = global::app.type.item.number.@this;
using PKind = global::app.type.item.number.NumberKind;

namespace PLang.Tests.App.LazyDeserialize.NumberTowerTests;

// Way 3 (Decision 5): replace the `_i/_d/_f` union with exact-CLR-type
// storage. The kind *is* the value's CLR type.
public class NumberStorageTests
{
    [Test] public async Task Number_OldUnionFieldsGone_iAnd_dAnd_f()
    {
        var t = typeof(number);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        await Assert.That(t.GetField("_i", flags)).IsNull();
        await Assert.That(t.GetField("_d", flags)).IsNull();
        await Assert.That(t.GetField("_f", flags)).IsNull();
    }

    [Test] public async Task Number_NumberKindEnum_Removed_OrRedefined()
    {
        // Redefined: the enum now spans the full tower (was Int/Long/Float/Double/Decimal).
        var names = System.Enum.GetNames(typeof(PKind));
        await Assert.That(names).Contains("UInt");
        await Assert.That(names).Contains("BigInteger");
        await Assert.That(names).Contains("Half");
        await Assert.That(names).Contains("Int128");
    }

    private static async Task StoredAs<T>(number n, T expected)
    {
        await Assert.That(n.BoxedValue).IsTypeOf<T>();
        await Assert.That((T)n.BoxedValue).IsEqualTo(expected);
    }

    [Test] public async Task Number_ExactClrValue_StoredVerbatim_Int() => await StoredAs(number.From(5), 5);
    [Test] public async Task Number_ExactClrValue_StoredVerbatim_UInt() => await StoredAs(number.From(5u), 5u);
    [Test] public async Task Number_ExactClrValue_StoredVerbatim_ULong() => await StoredAs(number.From(5ul), 5ul);
    [Test] public async Task Number_ExactClrValue_StoredVerbatim_Int128() => await StoredAs(number.From((Int128)5), (Int128)5);
    [Test] public async Task Number_ExactClrValue_StoredVerbatim_UInt128() => await StoredAs(number.From((UInt128)5), (UInt128)5);
    [Test] public async Task Number_ExactClrValue_StoredVerbatim_Half() => await StoredAs(number.From((Half)1.5), (Half)1.5);
    // Marquee: no float→double collapse.
    [Test] public async Task Number_ExactClrValue_StoredVerbatim_Float() => await StoredAs(number.From(1.5f), 1.5f);
    [Test] public async Task Number_ExactClrValue_StoredVerbatim_Decimal() => await StoredAs(number.From(1.5m), 1.5m);
    [Test] public async Task Number_ExactClrValue_StoredVerbatim_BigInteger() => await StoredAs(number.From((BigInteger)123), (BigInteger)123);

    [Test] public async Task Number_ExactClrValue_StoredVerbatim_Sbyte_Byte_Short_Ushort()
    {
        await StoredAs(number.From((sbyte)1), (sbyte)1);
        await StoredAs(number.From((byte)2), (byte)2);
        await StoredAs(number.From((short)3), (short)3);
        await StoredAs(number.From((ushort)4), (ushort)4);
    }
}
