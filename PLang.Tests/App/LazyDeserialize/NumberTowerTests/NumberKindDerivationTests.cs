using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.NumberTowerTests;

// "The kind *is* its type" — no separate label to drift. Derivation reads
// the value's `GetType()` and maps to the kind name. The build-stamp site
// at app/data/this.cs:242 currently collapses float→double; this branch
// removes that.
public class NumberKindDerivationTests
{
    [Test] public async Task Kind_DerivesFromValueClrType_Int_ProducesIntKind() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Kind_DerivesFromValueClrType_UInt_ProducesUIntKind() { throw new System.NotImplementedException("not implemented"); }

    // Independent #8 — float is not double. The kind is "float" when stored
    // as `float`, "double" when stored as `double`.
    [Test] public async Task Kind_DerivesFromValueClrType_Float_ProducesFloatKind_NotDouble() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Kind_DerivesFromValueClrType_BigInteger_ProducesBigIntegerKind() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task Kind_ForAllTowerEntries_RoundTripsThroughKindsList() { throw new System.NotImplementedException("not implemented"); }

    // The catalog. The architect lists the full tower:
    // sbyte byte short ushort int uint long ulong int128 uint128 half float
    // double decimal biginteger. Pin the advertised list.
    [Test] public async Task Kinds_AdvertisesFullTower() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task KindToClr_CoversFullTower() { throw new System.NotImplementedException("not implemented"); }

    // app/data/this.cs:242 today collapses float→double at stamp time. After
    // Stage 2 the stamp reflects the exact runtime type.
    [Test] public async Task BuildHook_StampsFromValueGetType_NoFloatCollapse() { throw new System.NotImplementedException("not implemented"); }
}
