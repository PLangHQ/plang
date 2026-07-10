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
        => await Assert.That(number.From(5).Kind).IsEqualTo(PKind.Int);

    [Test] public async Task Kind_DerivesFromValueClrType_UInt_ProducesUIntKind()
        => await Assert.That(number.From(5u).Kind).IsEqualTo(PKind.UInt);

    // Independent #8 — float is not double.
    [Test] public async Task Kind_DerivesFromValueClrType_Float_ProducesFloatKind_NotDouble()
    {
        await Assert.That(number.From(1.5f).Kind).IsEqualTo(PKind.Float);
        await Assert.That(number.From(1.5f).KindLabel).IsEqualTo("float");
        await Assert.That(number.From(1.5d).Kind).IsEqualTo(PKind.Double);
    }

    [Test] public async Task Kind_DerivesFromValueClrType_BigInteger_ProducesBigIntegerKind()
        => await Assert.That(number.From((BigInteger)1).Kind).IsEqualTo(PKind.BigInteger);

    [Test] public async Task Kind_ForAllTowerEntries_RoundTripsThroughKindsList()
    {
        foreach (var name in number.Kinds)
        {
            var k = number.KindFromName(name);
            await Assert.That(k).IsNotNull();
            await Assert.That(number.LabelOf(k!.Value)).IsEqualTo(name);
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
            await Assert.That(number.Kinds.Contains(name)).IsTrue();
    }

    [Test] public async Task KindToClr_CoversFullTower()
    {
        foreach (var name in number.Kinds)
            await Assert.That(number.KindToClrType(number.KindFromName(name))).IsNotNull();
    }

    // app/data/this.cs:242 no longer collapses float→double at stamp time.
    [Test] public async Task BuildHook_StampsFromValueGetType_NoFloatCollapse()
    {
        await Assert.That(number.Build(1.5f)).IsEqualTo("float");
        await Assert.That(number.Build(1.5d)).IsEqualTo("double");
        await Assert.That(number.Build(5u)).IsEqualTo("uint");
    }
}
