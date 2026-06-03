using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.NumberTowerTests;

// Way 3 (Decision 5): replace the `_i/_d/_f` union and the `NumberKind` enum
// with exact-CLR-type storage. The kind *is* the value's CLR type. These
// rows are the storage-shape pins; kind derivation, parsing, arithmetic
// sit in sibling files.
public class NumberStorageTests
{
    // app/type/number/this.cs:28–30 — the three union slots go away.
    [Test] public async Task Number_OldUnionFieldsGone_iAnd_dAnd_f() { throw new System.NotImplementedException("not implemented"); }

    // app/type/number/this.cs:25,216 — the old NumberKind enum either
    // disappears outright or stops being the canonical kind label.
    [Test] public async Task Number_NumberKindEnum_Removed_OrRedefined() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task Number_ExactClrValue_StoredVerbatim_Int() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Number_ExactClrValue_StoredVerbatim_UInt() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Number_ExactClrValue_StoredVerbatim_ULong() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Number_ExactClrValue_StoredVerbatim_Int128() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Number_ExactClrValue_StoredVerbatim_UInt128() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Number_ExactClrValue_StoredVerbatim_Half() { throw new System.NotImplementedException("not implemented"); }

    // Marquee row — the float→double collapse goes away. Stored value of
    // `float(1.5f)` is `float`, not `double`.
    [Test] public async Task Number_ExactClrValue_StoredVerbatim_Float() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Number_ExactClrValue_StoredVerbatim_Decimal() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Number_ExactClrValue_StoredVerbatim_BigInteger() { throw new System.NotImplementedException("not implemented"); }

    // Parametric across sbyte/byte/short/ushort. Small integer kinds the
    // language previously couldn't represent at all.
    [Test] public async Task Number_ExactClrValue_StoredVerbatim_Sbyte_Byte_Short_Ushort() { throw new System.NotImplementedException("not implemented"); }
}
