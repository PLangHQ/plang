using System.Linq;
using System.Numerics;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using number = global::app.type.item.number.@this;
using PKind = global::app.type.item.number.NumberKind;

namespace PLang.Tests.App.LazyDeserialize.NumberTowerTests;

// "The kind *is* its type" — no separate label to drift.
public class NumberKindDerivationTests
{
    [Test] public async Task Kind_DerivesFromValueClrType_Int_ProducesIntKind()
        => await Assert.That(((number)(5)).Kind.Name).IsEqualTo("int");

    [Test] public async Task Kind_DerivesFromValueClrType_UInt_ProducesUIntKind()
        => await Assert.That(((number)(5u)).Kind.Name).IsEqualTo("uint");

    // Independent #8 — float is not double.
    [Test] public async Task Kind_DerivesFromValueClrType_Float_ProducesFloatKind_NotDouble()
    {
        await Assert.That(((number)(1.5f)).Kind.Name).IsEqualTo("float");
        await Assert.That(((number)(1.5d)).Kind.Name).IsEqualTo("double");
    }

    [Test] public async Task Kind_DerivesFromValueClrType_BigInteger_ProducesBigIntegerKind()
        => await Assert.That(((number)((BigInteger)1)).Kind.Name).IsEqualTo("biginteger");

    [Test] public async Task Kind_ForAllTowerEntries_RoundTripsThroughKindsList()
    {
        foreach (var name in number.Kinds.Keys)
        {
            var k = number.Kinds[name];
            await Assert.That(k).IsNotNull();
            await Assert.That(k.Name).IsEqualTo(name);
        }
    }

    [Test] public async Task Kinds_AdvertisesFullTower()
    {
        string[] expected =
        {
            "sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong",
            "int128", "uint128", "half", "float", "double", "decimal", "biginteger",
        };
        foreach (var name in expected)
            await Assert.That(number.Kinds.ContainsKey(name)).IsTrue();
    }

    [Test] public async Task EveryKind_CreatesANumber_CoversFullTower()
    {
        // the kind IS its behavior — each kind Creates a number of its storage size from a plang value
        foreach (var name in number.Kinds.Keys)
            await Assert.That(number.Kinds[name].Create((number)1)).IsNotNull();
    }

    // app/data/this.cs:242 no longer collapses float→double at stamp time.
    [Test] public async Task BuildHook_StampsFromValueGetType_NoFloatCollapse()
    {
        await Assert.That(number.Build(1.5f)).IsEqualTo("float");
        await Assert.That(number.Build(1.5d)).IsEqualTo("double");
        await Assert.That(number.Build(5u)).IsEqualTo("uint");
    }
}
