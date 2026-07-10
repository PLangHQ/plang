using System.Linq;
using System.Numerics;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using number = global::app.type.item.number.@this;
using convert = global::app.type.convert.@this;

namespace PLang.Tests.App.LazyDeserialize.NumberTowerTests;

// The distributed-OwnerOf payoff, scoped to number. Adding uint/ulong/Int128/
// BigInteger touches only number's declaration — never the central convert.
public class NumberDeclaresClrTypesTests
{
    [Test] public async Task Number_DeclaresFullTowerCrlTypes()
    {
        var clrs = number.OwnedClrTypes.Select(o => o.Clr).ToArray();
        System.Type[] tower =
        {
            typeof(sbyte), typeof(byte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(Int128), typeof(UInt128), typeof(BigInteger),
            typeof(Half), typeof(float), typeof(double), typeof(decimal),
        };
        foreach (var t in tower)
            await Assert.That(clrs.Contains(t)).IsTrue();
    }

    // The composition picks up a number-declared CLR type (uint) without any
    // central switch: OwnerOf routes uint → number, and uint is in number's
    // own declaration — so the kind was added by editing number alone.
    [Test] public async Task Number_AddingNewCrlType_RequiresOnlyNumberEdit()
    {
        var (family, kind) = convert.OwnerOf(typeof(uint));
        await Assert.That(family).IsEqualTo(typeof(number));
        await Assert.That(kind).IsEqualTo("uint");
        await Assert.That(number.OwnedClrTypes.Any(o => o.Clr == typeof(uint))).IsTrue();
    }
}
