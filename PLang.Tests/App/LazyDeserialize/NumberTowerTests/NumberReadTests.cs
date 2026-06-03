using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.NumberTowerTests;

// Stage 2's reader-side parsing. number.Read parses a string toward its
// exact named kind (uint, biginteger, half, …) — no implicit widening, no
// silent narrowing. Under lazy, an untouched number off the wire is just
// its text carrying a kind hint, materialised on first touch.
public class NumberReadTests
{
    [Test] public async Task Read_NumberInt_FromString_PreservesInt() { throw new System.NotImplementedException("not implemented"); }

    // "3000000000" doesn't fit in `int`. Reading toward `uint` parses
    // losslessly; reading toward `int` errors.
    [Test] public async Task Read_NumberUInt_FromBigDecimalString_ProducesUInt() { throw new System.NotImplementedException("not implemented"); }

    // Independent — 22-digit string fits no fixed-width integer kind;
    // reading toward `biginteger` parses losslessly.
    [Test] public async Task Read_NumberBigInteger_From22DigitString_LossLess() { throw new System.NotImplementedException("not implemented"); }

    // Independent #8 — negative zero preserves sign across float/double/decimal
    // kinds. The union model historically lost sign on float→double widening.
    [Test] public async Task Read_NumberFloat_NegativeZero_PreservesSignAndKind() { throw new System.NotImplementedException("not implemented"); }

    // decimal's full 28-digit precision survives a read.
    [Test] public async Task Read_NumberDecimal_PrecisionPreserved_28Digits() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task Read_NumberHalf_FromString_PreservesHalf() { throw new System.NotImplementedException("not implemented"); }

    // Independent #9 — under lazy, a too-big-for-named-kind error fires at
    // materialisation time (first touch of `.Value`), not at read time. The
    // architect's "errors move to touch-time" rule, applied to numbers.
    [Test] public async Task Read_TooBigForNamedKind_ErrorsAtMaterialise_NotAtRead() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task Read_NonNumericString_ProducesTypedError() { throw new System.NotImplementedException("not implemented"); }
}
